using QueryPilot.Api.Features.Analytics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace QueryPilot.Api.Features.Ai;

public sealed class AiAnalyticsCoordinator(
    IAiService aiService,
    IAiIntentValidator intentValidator,
    IAnalyticsService analyticsService) : IAiAnalyticsCoordinator
{
    private static readonly JsonSerializerOptions AnalyticsJsonOptions =
        CreateAnalyticsJsonOptions();

    public async Task<AiAnalyticsExecutionResult> ExecuteAsync(
        string question,
        CancellationToken cancellationToken = default)
    {
        var parsedIntent = await aiService.UnderstandQuestionAsync(
            question,
            cancellationToken);

        if (parsedIntent.Analysis == AiAnalysisType.Unknown)
        {
            return AiAnalyticsExecutionResult.UnsupportedQuestion();
        }

        var evaluation = await intentValidator.EvaluateAsync(
            parsedIntent,
            cancellationToken);

        if (evaluation.NeedsClarification)
        {
            return AiAnalyticsExecutionResult.NeedsClarification(
                question,
                parsedIntent,
                evaluation.RequiredFields);
        }

        var intent = evaluation.Intent!;

        object data = intent.Analysis switch
        {
            AiAnalysisType.SalesSummary =>
                await analyticsService.GetSalesSummaryAsync(
                    intent.FromUtc,
                    intent.ToUtc,
                    cancellationToken),
            AiAnalysisType.SalesTrend =>
                await analyticsService.GetSalesTrendAsync(
                    intent.FromUtc,
                    intent.ToUtc,
                    intent.Granularity!.Value,
                    cancellationToken),
            AiAnalysisType.TopProducts =>
                await analyticsService.GetTopProductsAsync(
                    intent.FromUtc,
                    intent.ToUtc,
                    intent.Metric!.Value,
                    intent.Limit,
                    intent.CategoryId,
                    cancellationToken),
            AiAnalysisType.CategoryPerformance =>
                await analyticsService.GetCategoryPerformanceAsync(
                    intent.FromUtc,
                    intent.ToUtc,
                    cancellationToken),
            AiAnalysisType.ReturnAnalysis =>
                await analyticsService.GetReturnAnalyticsAsync(
                    intent.FromUtc,
                    intent.ToUtc,
                    cancellationToken),
            _ => throw new InvalidOperationException(
                "Validated AI intent contains an unsupported analysis type.")
        };

        var analyticsResultJson = JsonSerializer.Serialize(
            data,
            data.GetType(),
            AnalyticsJsonOptions);
        var explanation = await CreateExplanationAsync(
            question,
            analyticsResultJson,
            cancellationToken);

        return AiAnalyticsExecutionResult.Completed(intent, data, explanation);
    }

    private async Task<AiExplanationResponse> CreateExplanationAsync(
        string question,
        string analyticsResultJson,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await aiService.ExplainResultAsync(
                question,
                analyticsResultJson,
                cancellationToken);

            return AiExplanationGuard.Validate(result, analyticsResultJson);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            return AiExplanationResponse.Unavailable();
        }
    }

    private static JsonSerializerOptions CreateAnalyticsJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
        return options;
    }
}
