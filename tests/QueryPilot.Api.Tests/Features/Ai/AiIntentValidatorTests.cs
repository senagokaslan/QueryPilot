using Microsoft.EntityFrameworkCore;
using QueryPilot.Api.Common.Exceptions;
using QueryPilot.Api.Data;
using QueryPilot.Api.Features.Ai;
using QueryPilot.Api.Features.Analytics;
using QueryPilot.Api.Features.Analytics.Dtos;
using QueryPilot.Api.Features.Categories;
using QueryPilot.Api.Features.Products;
using Xunit;

namespace QueryPilot.Api.Tests.Features.Ai;

public sealed class AiIntentValidatorTests
{
    private static readonly DateTimeOffset FixedUtcNow =
        new(2026, 8, 23, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Valid_top_products_intent_resolves_period_and_existing_category()
    {
        await using var dbContext = CreateDbContext();
        dbContext.Categories.Add(new Category
        {
            Id = 42,
            Name = "Elektronik",
            IsActive = false,
            CreatedAt = FixedUtcNow.UtcDateTime
        });
        await dbContext.SaveChangesAsync();
        var validator = CreateValidator(dbContext);
        var intent = CreateIntent(
            AiAnalysisType.TopProducts,
            period: "son 3 ay",
            categoryName: "Elektronik",
            metric: AiTopProductsMetric.Quantity,
            limit: 5);

        var result = await validator.ValidateAsync(intent);

        Assert.Equal(new DateTimeOffset(2026, 5, 23, 12, 0, 0, TimeSpan.Zero), result.FromUtc);
        Assert.Equal(FixedUtcNow, result.ToUtc);
        Assert.Equal(42, result.CategoryId);
        Assert.Equal("Elektronik", result.CategoryName);
        Assert.Equal(TopProductsMetric.Quantity, result.Metric);
        Assert.Equal(5, result.Limit);
    }

    [Fact]
    public async Task Missing_category_is_rejected()
    {
        await using var dbContext = CreateDbContext();
        var validator = CreateValidator(dbContext);
        var intent = CreateIntent(
            AiAnalysisType.TopProducts,
            categoryName: "Olmayan kategori",
            metric: AiTopProductsMetric.Revenue);

        var exception = await Assert.ThrowsAsync<RequestValidationException>(() =>
            validator.ValidateAsync(intent));

        Assert.Contains(nameof(AiQuestionUnderstanding.CategoryName), exception.Errors.Keys);
    }

    [Fact]
    public async Task Valid_sales_trend_maps_explicit_UTC_range_and_granularity()
    {
        await using var dbContext = CreateDbContext();
        var validator = CreateValidator(dbContext);
        var intent = CreateIntent(
            AiAnalysisType.SalesTrend,
            period: null,
            from: "2026-01-01T03:00:00+03:00",
            to: "2026-03-01T03:00:00+03:00",
            granularity: AiGranularity.Monthly);

        var result = await validator.ValidateAsync(intent);

        Assert.Equal(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero), result.FromUtc);
        Assert.Equal(new DateTimeOffset(2026, 3, 1, 0, 0, 0, TimeSpan.Zero), result.ToUtc);
        Assert.Equal(AnalyticsGranularity.Monthly, result.Granularity);
        Assert.Null(result.Limit);
    }

    [Theory]
    [MemberData(nameof(InvalidIntentCases))]
    public async Task Invalid_intent_never_calls_analytics_service(
        AiQuestionUnderstanding intent)
    {
        await using var dbContext = CreateDbContext();
        var analytics = new FailingAnalyticsService();
        var coordinator = new AiAnalyticsCoordinator(
            new FakeAiService(
                intent,
                new AiResultExplanation("unused")),
            CreateValidator(dbContext),
            analytics);

        await Assert.ThrowsAsync<RequestValidationException>(() =>
            coordinator.ExecuteAsync("test question"));

        Assert.Equal(0, analytics.CallCount);
    }

    [Fact]
    public async Task Best_products_question_requests_metric_and_date_without_querying_analytics()
    {
        await using var dbContext = CreateDbContext();
        var analytics = new FailingAnalyticsService();
        var intent = CreateIntent(
            AiAnalysisType.TopProducts,
            period: null,
            metric: null);
        var originalQuestion = "En iyi ürünler hangileri?";
        var coordinator = new AiAnalyticsCoordinator(
            new FakeAiService(intent, new AiResultExplanation("unused")),
            CreateValidator(dbContext),
            analytics);

        var result = await coordinator.ExecuteAsync(originalQuestion);

        Assert.Equal(AiAnalyticsExecutionStatus.NeedsClarification, result.Status);
        Assert.Null(result.Data);
        Assert.Null(result.Intent);
        Assert.NotNull(result.Clarification);
        Assert.Equal(originalQuestion, result.Clarification.OriginalQuestion);
        Assert.Same(intent, result.Clarification.CurrentIntent);
        Assert.Contains(
            result.Clarification.RequiredFields,
            field => field.Field == "dateRange");
        var metricField = Assert.Single(
            result.Clarification.RequiredFields,
            field => field.Field == nameof(AiQuestionUnderstanding.Metric));
        Assert.Equal(
            "Ürünleri satış adedine göre mi, gelire göre mi sıralayalım?",
            metricField.Question);
        Assert.Equal(["quantity", "revenue"], metricField.AllowedValues);
        Assert.Contains("yeniden gönderin", result.Clarification.RetryInstruction);
        Assert.Equal(0, analytics.CallCount);
    }

    [Fact]
    public async Task Unknown_analysis_returns_clarification_without_querying_analytics()
    {
        await using var dbContext = CreateDbContext();
        var analytics = new FailingAnalyticsService();
        var coordinator = new AiAnalyticsCoordinator(
            new FakeAiService(
                CreateIntent(AiAnalysisType.Unknown),
                new AiResultExplanation("unused")),
            CreateValidator(dbContext),
            analytics);

        var result = await coordinator.ExecuteAsync("Bana yardımcı ol");

        Assert.Equal(AiAnalyticsExecutionStatus.NeedsClarification, result.Status);
        Assert.Contains(
            result.Clarification!.RequiredFields,
            field => field.Field == nameof(AiQuestionUnderstanding.Analysis));
        Assert.Equal(0, analytics.CallCount);
    }

    [Fact]
    public async Task Ambiguous_product_name_is_not_selected_randomly()
    {
        await using var dbContext = CreateDbContext();
        dbContext.Categories.Add(new Category
        {
            Id = 1,
            Name = "Telefon",
            IsActive = true,
            CreatedAt = FixedUtcNow.UtcDateTime
        });
        dbContext.Products.AddRange(
            CreateProduct(1, "Pro Telefon", "SKU-1"),
            CreateProduct(2, "Pro Telefon", "SKU-2"));
        await dbContext.SaveChangesAsync();
        var analytics = new FailingAnalyticsService();
        var coordinator = new AiAnalyticsCoordinator(
            new FakeAiService(
                CreateIntent(
                    AiAnalysisType.SalesSummary,
                    productName: "Pro Telefon"),
                new AiResultExplanation("unused")),
            CreateValidator(dbContext),
            analytics);

        var result = await coordinator.ExecuteAsync("Pro Telefon satışlarını göster");

        Assert.Equal(AiAnalyticsExecutionStatus.NeedsClarification, result.Status);
        var productField = Assert.Single(
            result.Clarification!.RequiredFields,
            field => field.Field == nameof(AiQuestionUnderstanding.ProductName));
        Assert.Contains("birden fazla", productField.Question);
        Assert.Equal(0, analytics.CallCount);
    }

    [Fact]
    public async Task Non_exact_category_name_is_not_selected_randomly()
    {
        await using var dbContext = CreateDbContext();
        dbContext.Categories.AddRange(
            new Category
            {
                Id = 1,
                Name = "Elektronik",
                IsActive = true,
                CreatedAt = FixedUtcNow.UtcDateTime
            },
            new Category
            {
                Id = 2,
                Name = "Elektrik",
                IsActive = true,
                CreatedAt = FixedUtcNow.UtcDateTime
            });
        await dbContext.SaveChangesAsync();
        var analytics = new FailingAnalyticsService();
        var coordinator = new AiAnalyticsCoordinator(
            new FakeAiService(
                CreateIntent(
                    AiAnalysisType.TopProducts,
                    categoryName: "Elektro",
                    metric: AiTopProductsMetric.Quantity),
                new AiResultExplanation("unused")),
            CreateValidator(dbContext),
            analytics);

        var result = await coordinator.ExecuteAsync("Elektro kategorisinin ürünleri");

        Assert.Equal(AiAnalyticsExecutionStatus.NeedsClarification, result.Status);
        Assert.Contains(
            result.Clarification!.RequiredFields,
            field => field.Field == nameof(AiQuestionUnderstanding.CategoryName));
        Assert.Equal(0, analytics.CallCount);
    }

    [Fact]
    public async Task Malformed_provider_output_never_calls_analytics_service()
    {
        await using var dbContext = CreateDbContext();
        var analytics = new FailingAnalyticsService();
        var aiService = new FakeAiService(
            (_, _) => Task.FromException<AiQuestionUnderstanding>(
                new AiServiceUnavailableException("Malformed JSON.")),
            (_, _, _) => Task.FromResult(new AiResultExplanation("unused")));
        var coordinator = new AiAnalyticsCoordinator(
            aiService,
            CreateValidator(dbContext),
            analytics);

        await Assert.ThrowsAsync<AiServiceUnavailableException>(() =>
            coordinator.ExecuteAsync("test question"));

        Assert.Equal(0, analytics.CallCount);
    }

    [Theory]
    [InlineData(AiAnalysisType.SalesTrend, null, null)]
    [InlineData(AiAnalysisType.TopProducts, null, null)]
    public async Task Analysis_specific_required_fields_are_enforced(
        AiAnalysisType analysis,
        AiGranularity? granularity,
        AiTopProductsMetric? metric)
    {
        await using var dbContext = CreateDbContext();
        var validator = CreateValidator(dbContext);
        var intent = CreateIntent(
            analysis,
            granularity: granularity,
            metric: metric);

        var exception = await Assert.ThrowsAsync<RequestValidationException>(() =>
            validator.ValidateAsync(intent));

        var expectedField = analysis == AiAnalysisType.SalesTrend
            ? nameof(AiQuestionUnderstanding.Granularity)
            : nameof(AiQuestionUnderstanding.Metric);
        Assert.Contains(expectedField, exception.Errors.Keys);
    }

    public static IEnumerable<object[]> InvalidIntentCases()
    {
        yield return [CreateIntent(
            AiAnalysisType.SalesSummary,
            period: null,
            from: "not-a-date",
            to: "2026-08-23")];
        yield return [CreateIntent(
            AiAnalysisType.SalesSummary,
            period: null,
            from: "2020-01-01",
            to: "2026-01-02")];
        yield return [CreateIntent(
            AiAnalysisType.TopProducts,
            metric: AiTopProductsMetric.Quantity,
            limit: 0)];
        yield return [CreateIntent(
            AiAnalysisType.TopProducts,
            metric: AiTopProductsMetric.Quantity,
            limit: 51)];
        yield return [CreateIntent(
            AiAnalysisType.SalesTrend,
            granularity: (AiGranularity)999)];
        yield return [CreateIntent(
            AiAnalysisType.TopProducts,
            metric: (AiTopProductsMetric)999)];
    }

    private static AiQuestionUnderstanding CreateIntent(
        AiAnalysisType analysis,
        string? period = "son ay",
        string? from = null,
        string? to = null,
        string? productName = null,
        string? categoryName = null,
        AiGranularity? granularity = null,
        AiTopProductsMetric? metric = null,
        int? limit = null) =>
        new(
            analysis,
            period,
            from,
            to,
            productName,
            categoryName,
            granularity,
            metric,
            limit);

    private static AiIntentValidator CreateValidator(AppDbContext dbContext) =>
        new(dbContext, new FixedTimeProvider(FixedUtcNow));

    private static AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"ai-intent-{Guid.NewGuid():N}")
            .Options;
        return new AppDbContext(options);
    }

    private static Product CreateProduct(long id, string name, string sku) =>
        new()
        {
            Id = id,
            Name = name,
            SKU = sku,
            CategoryId = 1,
            UnitPrice = 100,
            IsActive = true,
            CreatedAt = FixedUtcNow.UtcDateTime
        };

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }

    private sealed class FailingAnalyticsService : IAnalyticsService
    {
        public int CallCount { get; private set; }

        public AnalyticsDateRange CreateDateRange(DateTimeOffset from, DateTimeOffset to) =>
            throw UnexpectedCall();

        public Task<SalesSummaryResponse> GetSalesSummaryAsync(
            DateTimeOffset from,
            DateTimeOffset to,
            CancellationToken cancellationToken = default) =>
            Task.FromException<SalesSummaryResponse>(UnexpectedCall());

        public Task<SalesTrendResponse> GetSalesTrendAsync(
            DateTimeOffset from,
            DateTimeOffset to,
            AnalyticsGranularity granularity,
            CancellationToken cancellationToken = default) =>
            Task.FromException<SalesTrendResponse>(UnexpectedCall());

        public Task<TopProductsResponse> GetTopProductsAsync(
            DateTimeOffset from,
            DateTimeOffset to,
            TopProductsMetric metric,
            int? limit = null,
            long? categoryId = null,
            CancellationToken cancellationToken = default) =>
            Task.FromException<TopProductsResponse>(UnexpectedCall());

        public Task<CategoryPerformanceResponse> GetCategoryPerformanceAsync(
            DateTimeOffset from,
            DateTimeOffset to,
            CancellationToken cancellationToken = default) =>
            Task.FromException<CategoryPerformanceResponse>(UnexpectedCall());

        public Task<ReturnAnalyticsResponse> GetReturnAnalyticsAsync(
            DateTimeOffset from,
            DateTimeOffset to,
            CancellationToken cancellationToken = default) =>
            Task.FromException<ReturnAnalyticsResponse>(UnexpectedCall());

        private InvalidOperationException UnexpectedCall()
        {
            CallCount++;
            return new InvalidOperationException(
                "Analytics service must not be called for invalid AI intent.");
        }
    }
}
