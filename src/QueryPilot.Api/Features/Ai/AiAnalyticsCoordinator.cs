using QueryPilot.Api.Features.Analytics;

namespace QueryPilot.Api.Features.Ai;

public sealed class AiAnalyticsCoordinator(
    IAiService aiService,
    IAiIntentValidator intentValidator,
    IAnalyticsService analyticsService) : IAiAnalyticsCoordinator
{
    public async Task<AiAnalyticsExecutionResult> ExecuteAsync(
        string question,
        CancellationToken cancellationToken = default)
    {
        var parsedIntent = await aiService.UnderstandQuestionAsync(
            question,
            cancellationToken);
        var intent = await intentValidator.ValidateAsync(
            parsedIntent,
            cancellationToken);

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

        return new AiAnalyticsExecutionResult(intent, data);
    }
}
