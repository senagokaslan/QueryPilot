using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using QueryPilot.Api.Common.Exceptions;
using QueryPilot.Api.Configuration;

namespace QueryPilot.Api.Features.Ai;

public sealed class OpenAiService(
    HttpClient httpClient,
    IOptions<AiOptions> options,
    ILogger<OpenAiService> logger) : IAiService
{
    private const string ProviderName = "OpenAI";
    private const int MaximumAttempts = 3;

    private const string UnderstandInstructions =
        "Understand the user's analytics question. Return a concise interpretation " +
        "that can be converted into a backend analytics request. Do not calculate metrics.";

    private const string ExplainInstructions =
        "Explain the supplied backend analytics result clearly and concisely. " +
        "Use only the supplied result; do not invent or recalculate values.";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<AiQuestionUnderstanding> UnderstandQuestionAsync(
        string question,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(question);

        var text = await SendAsync(
            "understand-question",
            UnderstandInstructions,
            question,
            cancellationToken);

        return new AiQuestionUnderstanding(text);
    }

    public async Task<AiResultExplanation> ExplainResultAsync(
        string question,
        string analyticsResultJson,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(question);
        ArgumentException.ThrowIfNullOrWhiteSpace(analyticsResultJson);

        var input = $"Question:\n{question}\n\nBackend analytics result (JSON):\n{analyticsResultJson}";
        var text = await SendAsync(
            "explain-result",
            ExplainInstructions,
            input,
            cancellationToken);

        return new AiResultExplanation(text);
    }

    private async Task<string> SendAsync(
        string operation,
        string instructions,
        string input,
        CancellationToken cancellationToken)
    {
        var settings = ValidateOptions(options.Value);
        var startedAt = Stopwatch.GetTimestamp();
        var outcome = "failed";

        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken);
        timeoutSource.CancelAfter(TimeSpan.FromSeconds(settings.TimeoutSeconds));

        try
        {
            for (var attempt = 1; attempt <= MaximumAttempts; attempt++)
            {
                try
                {
                    using var request = CreateRequest(
                        settings,
                        instructions,
                        input);
                    using var response = await httpClient.SendAsync(
                        request,
                        HttpCompletionOption.ResponseHeadersRead,
                        timeoutSource.Token);

                    if (response.IsSuccessStatusCode)
                    {
                        var text = await ReadOutputTextAsync(
                            response,
                            timeoutSource.Token);
                        outcome = "succeeded";
                        return text;
                    }

                    if (!IsTransient(response.StatusCode)
                        || attempt == MaximumAttempts)
                    {
                        throw new AiServiceUnavailableException(
                            "The AI provider could not complete the request.");
                    }

                    await DelayBeforeRetryAsync(attempt, timeoutSource.Token);
                }
                catch (HttpRequestException)
                    when (attempt < MaximumAttempts)
                {
                    logger.LogWarning(
                        "Transient AI provider transport failure. Provider: {Provider}; " +
                        "Operation: {Operation}; Attempt: {Attempt}.",
                        settings.Provider,
                        operation,
                        attempt);
                    await DelayBeforeRetryAsync(attempt, timeoutSource.Token);
                }
            }

            throw new AiServiceUnavailableException(
                "The AI provider could not complete the request.");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            outcome = "canceled";
            throw;
        }
        catch (OperationCanceledException exception)
        {
            outcome = "timed-out";
            throw new AiServiceUnavailableException(
                "The AI provider request timed out.",
                exception);
        }
        catch (HttpRequestException exception)
        {
            throw new AiServiceUnavailableException(
                "The AI provider is temporarily unavailable.",
                exception);
        }
        finally
        {
            logger.LogInformation(
                "AI request completed. Provider: {Provider}; Model: {Model}; " +
                "Operation: {Operation}; Outcome: {Outcome}; ElapsedMs: {ElapsedMs}.",
                settings.Provider,
                settings.Model,
                operation,
                outcome,
                Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds);
        }
    }

    private static HttpRequestMessage CreateRequest(
        ValidatedAiOptions settings,
        string instructions,
        string input)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "responses")
        {
            Content = JsonContent.Create(
                new OpenAiResponseRequest(
                    settings.Model,
                    instructions,
                    input,
                    Store: false),
                options: JsonOptions)
        };
        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            settings.ApiKey);

        return request;
    }

    private static async Task<string> ReadOutputTextAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        var providerResponse = await response.Content.ReadFromJsonAsync<OpenAiResponse>(
            JsonOptions,
            cancellationToken);
        var text = providerResponse?.Output
            .SelectMany(item => item.Content ?? [])
            .FirstOrDefault(content => content.Type == "output_text")
            ?.Text
            ?.Trim();

        return !string.IsNullOrWhiteSpace(text)
            ? text
            : throw new AiServiceUnavailableException(
                "The AI provider returned an empty response.");
    }

    private static ValidatedAiOptions ValidateOptions(AiOptions settings)
    {
        if (!string.Equals(settings.Provider, ProviderName, StringComparison.OrdinalIgnoreCase))
        {
            throw new AiServiceUnavailableException(
                "The configured AI provider is not supported.");
        }

        if (string.IsNullOrWhiteSpace(settings.Model)
            || string.IsNullOrWhiteSpace(settings.ApiKey))
        {
            throw new AiServiceUnavailableException(
                "The AI provider is not configured.");
        }

        if (settings.TimeoutSeconds is < AiOptions.MinimumTimeoutSeconds
            or > AiOptions.MaximumTimeoutSeconds)
        {
            throw new AiServiceUnavailableException(
                $"AI timeout must be between {AiOptions.MinimumTimeoutSeconds} and " +
                $"{AiOptions.MaximumTimeoutSeconds} seconds.");
        }

        return new ValidatedAiOptions(
            settings.Provider.Trim(),
            settings.Model.Trim(),
            settings.ApiKey.Trim(),
            settings.TimeoutSeconds);
    }

    private static bool IsTransient(HttpStatusCode statusCode) =>
        statusCode is HttpStatusCode.RequestTimeout
            or HttpStatusCode.TooManyRequests
            or HttpStatusCode.InternalServerError
            or HttpStatusCode.BadGateway
            or HttpStatusCode.ServiceUnavailable
            or HttpStatusCode.GatewayTimeout;

    private static Task DelayBeforeRetryAsync(
        int attempt,
        CancellationToken cancellationToken) =>
        Task.Delay(TimeSpan.FromMilliseconds(200 * attempt), cancellationToken);

    private sealed record ValidatedAiOptions(
        string Provider,
        string Model,
        string ApiKey,
        int TimeoutSeconds);

    private sealed record OpenAiResponseRequest(
        string Model,
        string Instructions,
        string Input,
        bool Store);

    private sealed record OpenAiResponse(
        IReadOnlyList<OpenAiOutputItem> Output);

    private sealed record OpenAiOutputItem(
        IReadOnlyList<OpenAiOutputContent>? Content);

    private sealed record OpenAiOutputContent(
        string Type,
        string? Text);
}
