using System.Text.Json;
using QueryPilot.Api.Features.Ai;
using QueryPilot.Api.Features.Analytics;
using QueryPilot.Api.Features.Analytics.Dtos;
using Xunit;

namespace QueryPilot.Api.Tests.Features.Ai;

public sealed class AiAnalyticsExplanationTests
{
    private static readonly DateTimeOffset FromUtc =
        new(2026, 7, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset ToUtc =
        new(2026, 8, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Analytics_dto_is_sent_to_ai_and_decrease_explanation_is_optional()
    {
        var analyticsResponse = CreateSummary();
        string? capturedJson = null;
        var aiService = new FakeAiService(
            (_, _) => Task.FromResult(CreateUnderstanding()),
            (_, json, _) =>
            {
                capturedJson = json;
                return Task.FromResult(new AiResultExplanation(
                    "Gelir 610.000 TL'den 540.000 TL'ye gerileyerek azaldı."));
            });
        var coordinator = CreateCoordinator(aiService, analyticsResponse);

        var result = await coordinator.ExecuteAsync("Geçen aya göre satış nasıl?");

        Assert.Equal(AiAnalyticsExecutionStatus.Completed, result.Status);
        Assert.Same(analyticsResponse, result.Data);
        Assert.NotNull(capturedJson);
        using var document = JsonDocument.Parse(capturedJson);
        Assert.Equal(
            540000m,
            document.RootElement.GetProperty("totalRevenue").GetDecimal());
        Assert.Equal(
            610000m,
            document.RootElement
                .GetProperty("revenueComparison")
                .GetProperty("previousValue")
                .GetDecimal());
        Assert.Equal(AiExplanationStatus.Available, result.Explanation!.Status);
        Assert.Contains("azaldı", result.Explanation.Text);
    }

    [Fact]
    public async Task Ai_failure_does_not_discard_numeric_analytics_result()
    {
        var analyticsResponse = CreateSummary();
        var aiService = new FakeAiService(
            (_, _) => Task.FromResult(CreateUnderstanding()),
            (_, _, _) => Task.FromException<AiResultExplanation>(
                new InvalidOperationException("Provider failed.")));
        var coordinator = CreateCoordinator(aiService, analyticsResponse);

        var result = await coordinator.ExecuteAsync("Satış nasıl?");

        Assert.Equal(AiAnalyticsExecutionStatus.Completed, result.Status);
        Assert.Same(analyticsResponse, result.Data);
        Assert.Equal(AiExplanationStatus.Unavailable, result.Explanation!.Status);
        Assert.Null(result.Explanation.Text);
    }

    [Fact]
    public async Task Explanation_with_number_absent_from_dto_is_rejected_but_data_is_kept()
    {
        var analyticsResponse = CreateSummary();
        var aiService = new FakeAiService(
            (_, _) => Task.FromResult(CreateUnderstanding()),
            (_, _, _) => Task.FromResult(new AiResultExplanation(
                "Gelir 999.000 TL oldu.")));
        var coordinator = CreateCoordinator(aiService, analyticsResponse);

        var result = await coordinator.ExecuteAsync("Satış nasıl?");

        Assert.Equal(AiAnalyticsExecutionStatus.Completed, result.Status);
        Assert.Same(analyticsResponse, result.Data);
        Assert.Equal(AiExplanationStatus.RejectedUnsafe, result.Explanation!.Status);
        Assert.Null(result.Explanation.Text);
    }

    [Fact]
    public void Explanation_over_length_limit_is_rejected()
    {
        const string json = "{\"totalRevenue\":540000}";
        var explanation = new AiResultExplanation(
            new string('a', AiExplanationGuard.MaximumLength + 1));

        var result = AiExplanationGuard.Validate(explanation, json);

        Assert.Equal(AiExplanationStatus.RejectedUnsafe, result.Status);
        Assert.Null(result.Text);
    }

    private static AiAnalyticsCoordinator CreateCoordinator(
        IAiService aiService,
        SalesSummaryResponse response) =>
        new(
            aiService,
            new StubIntentValidator(CreateValidatedIntent()),
            new StubAnalyticsService(response));

    private static AiQuestionUnderstanding CreateUnderstanding() =>
        new(
            AiAnalysisType.SalesSummary,
            Period: null,
            From: FromUtc.ToString("O"),
            To: ToUtc.ToString("O"),
            ProductName: null,
            CategoryName: null,
            Granularity: null,
            Metric: null,
            Limit: null);

    private static ValidatedAnalyticsIntent CreateValidatedIntent() =>
        new(
            AiAnalysisType.SalesSummary,
            FromUtc,
            ToUtc,
            ProductId: null,
            ProductName: null,
            CategoryId: null,
            CategoryName: null,
            Granularity: null,
            Metric: null,
            Limit: null);

    private static SalesSummaryResponse CreateSummary() =>
        new(
            FromUtc,
            ToUtc,
            TotalRevenue: 540000m,
            OrderCount: 18,
            UnitsSold: 25,
            AverageOrderValue: 30000m,
            new ComparisonPeriodResponse(
                FromUtc,
                ToUtc,
                new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero),
                FromUtc,
                (ToUtc - FromUtc).Ticks,
                "Adjacent half-open UTC ranges with equal tick duration."),
            new MetricComparisonResponse(
                CurrentValue: 540000m,
                PreviousValue: 610000m,
                PercentageChange: -11.4754m,
                ComparisonStatus.Decrease));

    private sealed class StubIntentValidator(ValidatedAnalyticsIntent intent)
        : IAiIntentValidator
    {
        public Task<AiIntentEvaluationResult> EvaluateAsync(
            AiQuestionUnderstanding understanding,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(AiIntentEvaluationResult.Valid(intent));

        public Task<ValidatedAnalyticsIntent> ValidateAsync(
            AiQuestionUnderstanding understanding,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(intent);
    }

    private sealed class StubAnalyticsService(SalesSummaryResponse summary)
        : IAnalyticsService
    {
        public AnalyticsDateRange CreateDateRange(DateTimeOffset from, DateTimeOffset to) =>
            throw new NotSupportedException();

        public Task<SalesSummaryResponse> GetSalesSummaryAsync(
            DateTimeOffset from,
            DateTimeOffset to,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(summary);

        public Task<SalesTrendResponse> GetSalesTrendAsync(
            DateTimeOffset from,
            DateTimeOffset to,
            AnalyticsGranularity granularity,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<TopProductsResponse> GetTopProductsAsync(
            DateTimeOffset from,
            DateTimeOffset to,
            TopProductsMetric metric,
            int? limit = null,
            long? categoryId = null,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<CategoryPerformanceResponse> GetCategoryPerformanceAsync(
            DateTimeOffset from,
            DateTimeOffset to,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<ReturnAnalyticsResponse> GetReturnAnalyticsAsync(
            DateTimeOffset from,
            DateTimeOffset to,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
