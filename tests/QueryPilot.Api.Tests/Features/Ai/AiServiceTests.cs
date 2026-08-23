using System.Collections.Concurrent;
using System.Net;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using QueryPilot.Api.Common.Exceptions;
using QueryPilot.Api.Configuration;
using QueryPilot.Api.Features.Ai;
using Xunit;

namespace QueryPilot.Api.Tests.Features.Ai;

public sealed class AiServiceTests
{
    [Fact]
    public async Task Fake_service_returns_configured_application_contracts()
    {
        IAiService service = new FakeAiService(
            new AiQuestionUnderstanding("sales-summary"),
            new AiResultExplanation("Revenue increased."));

        var understanding = await service.UnderstandQuestionAsync("Show sales");
        var explanation = await service.ExplainResultAsync(
            "Show sales",
            "{\"totalRevenue\":100}");

        Assert.Equal("sales-summary", understanding.Text);
        Assert.Equal("Revenue increased.", explanation.Text);
    }

    [Fact]
    public async Task OpenAI_adapter_maps_response_without_leaking_provider_contracts()
    {
        var testCredential = $"test-{Guid.NewGuid():N}";
        var handler = new StubHttpMessageHandler((request, _) =>
        {
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.Equal("https://api.openai.com/v1/responses", request.RequestUri?.ToString());
            Assert.Equal("Bearer", request.Headers.Authorization?.Scheme);
            Assert.Equal(testCredential, request.Headers.Authorization?.Parameter);

            return Task.FromResult(JsonResponse(
                "{\"output\":[{\"content\":[{\"type\":\"output_text\",\"text\":\"sales summary\"}]}]}"));
        });
        var logger = new CapturingLogger<OpenAiService>();
        var service = CreateService(handler, logger, credential: testCredential);

        var result = await service.UnderstandQuestionAsync("sensitive customer question");

        Assert.Equal("sales summary", result.Text);
        Assert.DoesNotContain(testCredential, logger.MessagesText);
        Assert.DoesNotContain("sensitive customer question", logger.MessagesText);
        Assert.Contains("ElapsedMs", logger.MessagesText);
    }

    [Fact]
    public async Task OpenAI_adapter_retries_transient_provider_failures()
    {
        var attempts = 0;
        var handler = new StubHttpMessageHandler((_, _) =>
        {
            attempts++;
            return Task.FromResult(attempts switch
            {
                1 => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable),
                2 => new HttpResponseMessage(HttpStatusCode.TooManyRequests),
                _ => JsonResponse(
                    "{\"output\":[{\"content\":[{\"type\":\"output_text\",\"text\":\"done\"}]}]}")
            });
        });
        var service = CreateService(handler, new CapturingLogger<OpenAiService>());

        var result = await service.UnderstandQuestionAsync("Show sales");

        Assert.Equal("done", result.Text);
        Assert.Equal(3, attempts);
    }

    [Fact]
    public async Task OpenAI_adapter_propagates_caller_cancellation()
    {
        var handler = new StubHttpMessageHandler(async (_, cancellationToken) =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return JsonResponse("{}");
        });
        var service = CreateService(handler, new CapturingLogger<OpenAiService>());
        using var cancellationSource = new CancellationTokenSource();
        cancellationSource.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            service.UnderstandQuestionAsync(
                "Show sales",
                cancellationSource.Token));
    }

    [Fact]
    public async Task OpenAI_adapter_converts_timeout_to_service_unavailable()
    {
        var handler = new StubHttpMessageHandler(async (_, cancellationToken) =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return JsonResponse("{}");
        });
        var service = CreateService(
            handler,
            new CapturingLogger<OpenAiService>(),
            timeoutSeconds: 1);

        var exception = await Assert.ThrowsAsync<AiServiceUnavailableException>(() =>
            service.UnderstandQuestionAsync("Show sales"));

        Assert.Contains("timed out", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static OpenAiService CreateService(
        HttpMessageHandler handler,
        ILogger<OpenAiService> logger,
        int timeoutSeconds = 5,
        string? credential = null)
    {
        var client = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.openai.com/v1/"),
            Timeout = Timeout.InfiniteTimeSpan
        };
        var options = Options.Create(new AiOptions
        {
            Provider = "OpenAI",
            Model = "test-model",
            ApiKey = credential ?? $"test-{Guid.NewGuid():N}",
            TimeoutSeconds = timeoutSeconds
        });

        return new OpenAiService(client, options, logger);
    }

    private static HttpResponseMessage JsonResponse(string json) =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

    private sealed class StubHttpMessageHandler(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            send(request, cancellationToken);
    }

    private sealed class CapturingLogger<T> : ILogger<T>
    {
        private readonly ConcurrentQueue<string> messages = new();

        public string MessagesText => string.Join(Environment.NewLine, messages);

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter) =>
            messages.Enqueue(formatter(state, exception));
    }
}
