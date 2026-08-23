using QueryPilot.Api.Features.Ai;
using QueryPilot.Api.Features.Analytics;
using QueryPilot.Api.Features.Analytics.Dtos;
using Xunit;

namespace QueryPilot.Api.Tests.Features.Ai;

public sealed class AiAnalyticsRoutingTests
{
    private static readonly DateTimeOffset FromUtc =
        new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset ToUtc =
        new(2026, 4, 1, 0, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(AiAnalysisType.SalesSummary, "sales-summary")]
    [InlineData(AiAnalysisType.SalesTrend, "sales-trend")]
    [InlineData(AiAnalysisType.TopProducts, "top-products")]
    [InlineData(AiAnalysisType.CategoryPerformance, "category-performance")]
    [InlineData(AiAnalysisType.ReturnAnalysis, "return-analysis")]
    public async Task Each_analysis_calls_exactly_one_expected_analytics_method(
        AiAnalysisType analysis,
        string expectedOperation)
    {
        var intent = CreateIntent(analysis);
        var analytics = new RecordingAnalyticsService();
        var aiService = new FakeAiService(
            CreateUnderstanding(analysis),
            new AiResultExplanation("Sonuç hazır."));
        var coordinator = new AiAnalyticsCoordinator(
            aiService,
            new StubIntentValidator(intent),
            analytics);

        var result = await coordinator.ExecuteAsync("Test sorusu");

        Assert.Equal(AiAnalyticsExecutionStatus.Completed, result.Status);
        Assert.Equal(1, analytics.CallCount);
        Assert.Equal(expectedOperation, analytics.LastOperation);
        Assert.Equal(FromUtc, analytics.LastFromUtc);
        Assert.Equal(ToUtc, analytics.LastToUtc);

        if (analysis == AiAnalysisType.SalesTrend)
        {
            Assert.Equal(AnalyticsGranularity.Weekly, analytics.LastGranularity);
        }

        if (analysis == AiAnalysisType.TopProducts)
        {
            Assert.Equal(TopProductsMetric.Revenue, analytics.LastMetric);
            Assert.Equal(7, analytics.LastLimit);
            Assert.Equal(42, analytics.LastCategoryId);
        }
    }

    private static AiQuestionUnderstanding CreateUnderstanding(
        AiAnalysisType analysis) =>
        new(
            analysis,
            Period: "son 3 ay",
            From: null,
            To: null,
            ProductName: null,
            CategoryName: analysis == AiAnalysisType.TopProducts
                ? "Elektronik"
                : null,
            Granularity: analysis == AiAnalysisType.SalesTrend
                ? AiGranularity.Weekly
                : null,
            Metric: analysis == AiAnalysisType.TopProducts
                ? AiTopProductsMetric.Revenue
                : null,
            Limit: analysis == AiAnalysisType.TopProducts ? 7 : null);

    private static ValidatedAnalyticsIntent CreateIntent(AiAnalysisType analysis) =>
        new(
            analysis,
            FromUtc,
            ToUtc,
            ProductId: null,
            ProductName: null,
            CategoryId: analysis == AiAnalysisType.TopProducts ? 42 : null,
            CategoryName: analysis == AiAnalysisType.TopProducts
                ? "Elektronik"
                : null,
            Granularity: analysis == AiAnalysisType.SalesTrend
                ? AnalyticsGranularity.Weekly
                : null,
            Metric: analysis == AiAnalysisType.TopProducts
                ? TopProductsMetric.Revenue
                : null,
            Limit: analysis == AiAnalysisType.TopProducts ? 7 : null);

    private static ComparisonPeriodResponse CreateComparisonPeriod() =>
        new(
            FromUtc,
            ToUtc,
            FromUtc.AddMonths(-3),
            FromUtc,
            (ToUtc - FromUtc).Ticks,
            "Adjacent half-open UTC ranges with equal tick duration.");

    private static MetricComparisonResponse CreateMetricComparison() =>
        new(0m, 0m, PercentageChange: null, ComparisonStatus.NoBaseline);

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

    private sealed class RecordingAnalyticsService : IAnalyticsService
    {
        public int CallCount { get; private set; }

        public string? LastOperation { get; private set; }

        public DateTimeOffset? LastFromUtc { get; private set; }

        public DateTimeOffset? LastToUtc { get; private set; }

        public AnalyticsGranularity? LastGranularity { get; private set; }

        public TopProductsMetric? LastMetric { get; private set; }

        public int? LastLimit { get; private set; }

        public long? LastCategoryId { get; private set; }

        public AnalyticsDateRange CreateDateRange(DateTimeOffset from, DateTimeOffset to) =>
            throw new NotSupportedException();

        public Task<SalesSummaryResponse> GetSalesSummaryAsync(
            DateTimeOffset from,
            DateTimeOffset to,
            CancellationToken cancellationToken = default)
        {
            Record("sales-summary", from, to);
            return Task.FromResult(new SalesSummaryResponse(
                from,
                to,
                TotalRevenue: 0m,
                OrderCount: 0,
                UnitsSold: 0,
                AverageOrderValue: 0m,
                CreateComparisonPeriod(),
                CreateMetricComparison()));
        }

        public Task<SalesTrendResponse> GetSalesTrendAsync(
            DateTimeOffset from,
            DateTimeOffset to,
            AnalyticsGranularity granularity,
            CancellationToken cancellationToken = default)
        {
            Record("sales-trend", from, to);
            LastGranularity = granularity;
            return Task.FromResult(new SalesTrendResponse(
                from,
                to,
                granularity,
                Revenue: [],
                OrderCount: [],
                UnitsSold: []));
        }

        public Task<TopProductsResponse> GetTopProductsAsync(
            DateTimeOffset from,
            DateTimeOffset to,
            TopProductsMetric metric,
            int? limit = null,
            long? categoryId = null,
            CancellationToken cancellationToken = default)
        {
            Record("top-products", from, to);
            LastMetric = metric;
            LastLimit = limit;
            LastCategoryId = categoryId;
            return Task.FromResult(new TopProductsResponse(
                from,
                to,
                metric,
                limit!.Value,
                categoryId,
                Items: []));
        }

        public Task<CategoryPerformanceResponse> GetCategoryPerformanceAsync(
            DateTimeOffset from,
            DateTimeOffset to,
            CancellationToken cancellationToken = default)
        {
            Record("category-performance", from, to);
            return Task.FromResult(new CategoryPerformanceResponse(
                from,
                to,
                TotalRevenue: 0m,
                Items: [],
                CreateComparisonPeriod(),
                CreateMetricComparison()));
        }

        public Task<ReturnAnalyticsResponse> GetReturnAnalyticsAsync(
            DateTimeOffset from,
            DateTimeOffset to,
            CancellationToken cancellationToken = default)
        {
            Record("return-analysis", from, to);
            return Task.FromResult(new ReturnAnalyticsResponse(
                from,
                to,
                ReturnRecordCount: 0,
                ReturnedQuantity: 0,
                TotalReturnAmount: 0m,
                SoldQuantity: 0,
                ReturnRatePercentage: null,
                ReturnRateStatus.NoSalesBaseline,
                Reasons: [],
                MostReturnedProducts: [],
                new ReturnAnalyticsMetadataResponse(
                    "Returned quantity / sold quantity x 100.",
                    "Filtered by return date.",
                    "Filtered by order date.",
                    MostReturnedProductsLimit: 5)));
        }

        private void Record(
            string operation,
            DateTimeOffset from,
            DateTimeOffset to)
        {
            CallCount++;
            LastOperation = operation;
            LastFromUtc = from;
            LastToUtc = to;
        }
    }
}
