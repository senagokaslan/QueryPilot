using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QueryPilot.Api.Data;
using QueryPilot.Api.Features.Ai;
using QueryPilot.Api.Features.Analytics;
using QueryPilot.Api.Features.Analytics.Dtos;
using QueryPilot.Api.Features.Categories;
using QueryPilot.Api.Features.Customers;
using QueryPilot.Api.Features.Orders;
using QueryPilot.Api.Features.Products;
using ReturnEntity = QueryPilot.Api.Features.Returns.Return;
using Xunit;

namespace QueryPilot.Api.Tests.Features.Ai;

public sealed class AiEndToEndFlowTests
{
    private static readonly DateTimeOffset FixedUtcNow =
        new(2026, 8, 23, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Supported_Turkish_questions_match_direct_analytics_results()
    {
        await using var dbContext = CreateDbContext();
        await SeedKnownDataAsync(dbContext, FixedUtcNow, firstId: 1, prefix: string.Empty);
        await AssertSupportedQuestionsAsync(dbContext, FixedUtcNow, "Kupa");
    }

    [Fact]
    public async Task PostgreSQL_results_match_AI_flow_without_persisting_test_data()
    {
        var connectionString = Environment.GetEnvironmentVariable(
            "QUERYPILOT_POSTGRES_TEST_CONNECTION_STRING");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return;
        }

        var postgresUtcNow = new DateTimeOffset(
            2099,
            8,
            23,
            12,
            0,
            0,
            TimeSpan.Zero);
        var prefix = $"E2E-{Guid.NewGuid():N}-";
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(connectionString)
            .Options;
        await using var dbContext = new AppDbContext(options);
        await using var transaction = await dbContext.Database.BeginTransactionAsync();

        try
        {
            await SeedKnownDataAsync(
                dbContext,
                postgresUtcNow,
                firstId: -DateTime.UtcNow.Ticks,
                prefix);
            await AssertSupportedQuestionsAsync(
                dbContext,
                postgresUtcNow,
                $"{prefix}Kupa");
        }
        finally
        {
            await transaction.RollbackAsync();
        }

        await using var verificationContext = new AppDbContext(options);
        Assert.False(await verificationContext.Categories.AnyAsync(
            category => category.Name.StartsWith(prefix)));
    }

    private static async Task AssertSupportedQuestionsAsync(
        AppDbContext dbContext,
        DateTimeOffset utcNow,
        string expectedTopProductName)
    {
        var analytics = new AnalyticsService(dbContext);
        var recordingAnalytics = new RecordingAnalyticsService(analytics);
        var questions = CreateQuestionCases(utcNow);
        var understandingByQuestion = questions.ToDictionary(
            item => item.Question,
            item => item.Understanding);
        var explanationJsonByQuestion = new Dictionary<string, string>();
        var aiService = new FakeAiService(
            (question, _) => Task.FromResult(understandingByQuestion[question]),
            (question, analyticsJson, _) =>
            {
                explanationJsonByQuestion[question] = analyticsJson;
                var numericValue = FindFirstNumber(
                    JsonDocument.Parse(analyticsJson).RootElement);
                return Task.FromResult(new AiResultExplanation(
                    $"Backend sonucu {numericValue} değerini gösteriyor."));
            });
        var coordinator = new AiAnalyticsCoordinator(
            aiService,
            new AiIntentValidator(dbContext, new FixedTimeProvider(utcNow)),
            recordingAnalytics);
        var directController = new AnalyticsController(recordingAnalytics);

        foreach (var questionCase in questions)
        {
            recordingAnalytics.Clear();

            var aiResult = await coordinator.ExecuteAsync(questionCase.Question);

            Assert.Equal(AiAnalyticsExecutionStatus.Completed, aiResult.Status);
            Assert.Equal(questionCase.Analysis, aiResult.Intent!.Analysis);
            Assert.Equal(questionCase.ExpectedFromUtc, aiResult.Intent.FromUtc);
            Assert.Equal(utcNow, aiResult.Intent.ToUtc);
            Assert.Equal(questionCase.ExpectedMetric, aiResult.Intent.Metric);
            Assert.Equal(questionCase.ExpectedLimit, aiResult.Intent.Limit);
            Assert.Equal([questionCase.ExpectedOperation], recordingAnalytics.Calls);

            var directResult = await ExecuteDirectAsync(
                directController,
                aiResult.Intent);
            Assert.Equal(
                Serialize(directResult),
                Serialize(aiResult.Data!));
            AssertKnownFixtureValues(directResult, expectedTopProductName);
            Assert.Equal(
                [questionCase.ExpectedOperation, questionCase.ExpectedOperation],
                recordingAnalytics.Calls);

            var explanationJson = explanationJsonByQuestion[questionCase.Question];
            Assert.Equal(Serialize(aiResult.Data!), explanationJson);
            Assert.Equal(AiExplanationStatus.Available, aiResult.Explanation!.Status);
            Assert.Equal(
                AiExplanationStatus.Available,
                AiExplanationGuard.Validate(
                    new AiResultExplanation(aiResult.Explanation.Text!),
                    Serialize(directResult)).Status);
        }
    }

    [Fact]
    public async Task Ambiguous_question_returns_clarification_with_zero_analytics_calls()
    {
        await using var dbContext = CreateDbContext();
        var recordingAnalytics = new RecordingAnalyticsService(
            new AnalyticsService(dbContext));
        var understanding = new AiQuestionUnderstanding(
            AiAnalysisType.TopProducts,
            Period: null,
            From: null,
            To: null,
            ProductName: null,
            CategoryName: null,
            Granularity: null,
            Metric: null,
            Limit: null);
        var coordinator = new AiAnalyticsCoordinator(
            new FakeAiService(
                understanding,
                new AiResultExplanation("Kullanılmamalı.")),
            new AiIntentValidator(dbContext, new FixedTimeProvider(FixedUtcNow)),
            recordingAnalytics);

        var result = await coordinator.ExecuteAsync("En iyi ürünler hangileri?");

        Assert.Equal(AiAnalyticsExecutionStatus.NeedsClarification, result.Status);
        Assert.Contains(
            result.Clarification!.RequiredFields,
            field => field.Field == "dateRange");
        Assert.Contains(
            result.Clarification.RequiredFields,
            field => field.Field == nameof(AiQuestionUnderstanding.Metric));
        Assert.Empty(recordingAnalytics.Calls);
    }

    private static IReadOnlyList<QueryCase> CreateQuestionCases(
        DateTimeOffset utcNow)
    {
        var monthStart = new DateTimeOffset(
            utcNow.Year,
            utcNow.Month,
            1,
            0,
            0,
            0,
            TimeSpan.Zero);

        return
    [
        new QueryCase(
            "Bu ay satışlarımız nasıl?",
            new AiQuestionUnderstanding(
                AiAnalysisType.SalesSummary,
                "bu ay",
                From: null,
                To: null,
                ProductName: null,
                CategoryName: null,
                Granularity: null,
                Metric: null,
                Limit: null),
            AiAnalysisType.SalesSummary,
            monthStart,
            ExpectedMetric: null,
            ExpectedLimit: null,
            "sales-summary"),
        new QueryCase(
            "Son 3 ayda en çok satan 5 ürün ne?",
            new AiQuestionUnderstanding(
                AiAnalysisType.TopProducts,
                "son 3 ay",
                From: null,
                To: null,
                ProductName: null,
                CategoryName: null,
                Granularity: null,
                AiTopProductsMetric.Quantity,
                Limit: 5),
            AiAnalysisType.TopProducts,
            utcNow.AddMonths(-3),
            TopProductsMetric.Quantity,
            ExpectedLimit: 5,
            "top-products"),
        new QueryCase(
            "Bu ay kategoriler geçen döneme göre nasıl performans gösterdi?",
            new AiQuestionUnderstanding(
                AiAnalysisType.CategoryPerformance,
                "bu ay",
                From: null,
                To: null,
                ProductName: null,
                CategoryName: null,
                Granularity: null,
                Metric: null,
                Limit: null),
            AiAnalysisType.CategoryPerformance,
            monthStart,
            ExpectedMetric: null,
            ExpectedLimit: null,
            "category-performance"),
        new QueryCase(
            "Son 30 gündeki iadeleri analiz et.",
            new AiQuestionUnderstanding(
                AiAnalysisType.ReturnAnalysis,
                "son 30 gün",
                From: null,
                To: null,
                ProductName: null,
                CategoryName: null,
                Granularity: null,
                Metric: null,
                Limit: null),
            AiAnalysisType.ReturnAnalysis,
            utcNow.AddDays(-30),
            ExpectedMetric: null,
            ExpectedLimit: null,
            "return-analysis")
    ];
    }

    private static async Task<object> ExecuteDirectAsync(
        AnalyticsController controller,
        ValidatedAnalyticsIntent intent)
    {
        return intent.Analysis switch
        {
            AiAnalysisType.SalesSummary => ReadOkValue(
                await controller.GetSummary(
                    new AnalyticsDateRangeRequest
                    {
                        From = intent.FromUtc,
                        To = intent.ToUtc
                    },
                    CancellationToken.None)),
            AiAnalysisType.TopProducts => ReadOkValue(
                await controller.GetTopProducts(
                    new TopProductsRequest
                    {
                        From = intent.FromUtc,
                        To = intent.ToUtc,
                        Metric = intent.Metric,
                        Limit = intent.Limit,
                        CategoryId = intent.CategoryId
                    },
                    CancellationToken.None)),
            AiAnalysisType.CategoryPerformance => ReadOkValue(
                await controller.GetCategories(
                    new AnalyticsDateRangeRequest
                    {
                        From = intent.FromUtc,
                        To = intent.ToUtc
                    },
                    CancellationToken.None)),
            AiAnalysisType.ReturnAnalysis => ReadOkValue(
                await controller.GetReturns(
                    new AnalyticsDateRangeRequest
                    {
                        From = intent.FromUtc,
                        To = intent.ToUtc
                    },
                    CancellationToken.None)),
            _ => throw new InvalidOperationException("Unsupported test analysis.")
        };
    }

    private static object ReadOkValue<T>(ActionResult<T> actionResult) =>
        Assert.IsType<OkObjectResult>(actionResult.Result).Value!;

    private static void AssertKnownFixtureValues(
        object result,
        string expectedTopProductName)
    {
        switch (result)
        {
            case SalesSummaryResponse summary:
                Assert.Equal(1100m, summary.TotalRevenue);
                Assert.Equal(2, summary.OrderCount);
                Assert.Equal(12, summary.UnitsSold);
                Assert.Equal(600m, summary.RevenueComparison.PreviousValue);
                break;
            case TopProductsResponse products:
                var topProduct = Assert.Single(
                    products.Items,
                    product => product.Rank == 1);
                Assert.Equal(expectedTopProductName, topProduct.Name);
                Assert.Equal(6, topProduct.Quantity);
                break;
            case CategoryPerformanceResponse categories:
                Assert.Equal(1100m, categories.TotalRevenue);
                Assert.Equal(600m, categories.TotalRevenueComparison.PreviousValue);
                Assert.Equal(
                    ComparisonStatus.Increase,
                    categories.TotalRevenueComparison.Status);
                break;
            case ReturnAnalyticsResponse returns:
                Assert.Equal(2, returns.ReturnRecordCount);
                Assert.Equal(3, returns.ReturnedQuantity);
                Assert.Equal(300m, returns.TotalReturnAmount);
                Assert.Equal(12, returns.SoldQuantity);
                Assert.Equal(25m, returns.ReturnRatePercentage);
                break;
            default:
                throw new InvalidOperationException(
                    $"Unexpected direct analytics result: {result.GetType().Name}.");
        }
    }

    private static string Serialize(object value)
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };
        options.Converters.Add(
            new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
        return JsonSerializer.Serialize(value, value.GetType(), options);
    }

    private static string FindFirstNumber(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Number)
        {
            return element.GetRawText();
        }

        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                var value = FindFirstNumber(property.Value);
                if (value.Length > 0)
                {
                    return value;
                }
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                var value = FindFirstNumber(item);
                if (value.Length > 0)
                {
                    return value;
                }
            }
        }

        return string.Empty;
    }

    private static AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"ai-e2e-{Guid.NewGuid():N}")
            .Options;
        return new AppDbContext(options);
    }

    private static async Task SeedKnownDataAsync(
        AppDbContext dbContext,
        DateTimeOffset utcNow,
        long firstId,
        string prefix)
    {
        var createdAt = utcNow.UtcDateTime;
        var monthStart = new DateTimeOffset(
            utcNow.Year,
            utcNow.Month,
            1,
            0,
            0,
            0,
            TimeSpan.Zero);
        var electronics = new Category
        {
            Id = IdAt(firstId, 0),
            Name = $"{prefix}Elektronik",
            IsActive = true,
            CreatedAt = createdAt
        };
        var home = new Category
        {
            Id = IdAt(firstId, 1),
            Name = $"{prefix}Ev",
            IsActive = true,
            CreatedAt = createdAt
        };
        var phone = CreateProduct(
            IdAt(firstId, 0),
            $"{prefix}Telefon",
            $"{prefix}TEL-1",
            electronics,
            100m,
            createdAt);
        var laptop = CreateProduct(
            IdAt(firstId, 1),
            $"{prefix}Laptop",
            $"{prefix}LAP-1",
            electronics,
            300m,
            createdAt);
        var mug = CreateProduct(
            IdAt(firstId, 2),
            $"{prefix}Kupa",
            $"{prefix}KUP-1",
            home,
            50m,
            createdAt);
        var customer = new Customer
        {
            Id = IdAt(firstId, 0),
            Name = $"{prefix}Test Customer",
            Email = $"{prefix.ToLowerInvariant()}test@example.com",
            NormalizedEmail = $"{prefix.ToLowerInvariant()}test@example.com",
            City = "Ankara",
            IsActive = true,
            CreatedAt = createdAt
        };
        var augustOrderOne = CreateOrder(
            IdAt(firstId, 0), customer, monthStart.AddDays(4).AddHours(10).UtcDateTime, 800m);
        var augustOrderTwo = CreateOrder(
            IdAt(firstId, 1), customer, monthStart.AddDays(9).AddHours(10).UtcDateTime, 300m);
        var julyOrder = CreateOrder(
            IdAt(firstId, 2), customer, monthStart.AddDays(-17).AddHours(10).UtcDateTime, 600m);
        var cancelledOrder = CreateOrder(
            IdAt(firstId, 3),
            customer,
            monthStart.AddDays(14).AddHours(10).UtcDateTime,
            999m,
            OrderStatus.Cancelled);
        var phoneItem = CreateOrderItem(
            IdAt(firstId, 0), augustOrderOne, phone, quantity: 5, unitPrice: 100m);
        var laptopItem = CreateOrderItem(
            IdAt(firstId, 1), augustOrderOne, laptop, quantity: 1, unitPrice: 300m);
        var mugItem = CreateOrderItem(
            IdAt(firstId, 2), augustOrderTwo, mug, quantity: 6, unitPrice: 50m);
        var julyLaptopItem = CreateOrderItem(
            IdAt(firstId, 3), julyOrder, laptop, quantity: 2, unitPrice: 300m);
        var cancelledItem = CreateOrderItem(
            IdAt(firstId, 4), cancelledOrder, phone, quantity: 9, unitPrice: 111m);
        var returnOne = CreateReturn(
            IdAt(firstId, 0),
            phoneItem,
            quantity: 2,
            amount: 200m,
            "Hasarlı",
            monthStart.AddDays(11).AddHours(9).UtcDateTime);
        var returnTwo = CreateReturn(
            IdAt(firstId, 1),
            phoneItem,
            quantity: 1,
            amount: 100m,
            "Diğer",
            monthStart.AddDays(13).AddHours(9).UtcDateTime);

        dbContext.AddRange(
            electronics,
            home,
            phone,
            laptop,
            mug,
            customer,
            augustOrderOne,
            augustOrderTwo,
            julyOrder,
            cancelledOrder,
            phoneItem,
            laptopItem,
            mugItem,
            julyLaptopItem,
            cancelledItem,
            returnOne,
            returnTwo);
        await dbContext.SaveChangesAsync();
    }

    private static Product CreateProduct(
        long id,
        string name,
        string sku,
        Category category,
        decimal unitPrice,
        DateTime createdAt) =>
        new()
        {
            Id = id,
            Name = name,
            SKU = sku,
            CategoryId = category.Id,
            Category = category,
            UnitPrice = unitPrice,
            IsActive = true,
            CreatedAt = createdAt
        };

    private static long IdAt(long firstId, int offset) =>
        firstId >= 0 ? firstId + offset : firstId - offset;

    private static Order CreateOrder(
        long id,
        Customer customer,
        DateTime orderDate,
        decimal totalAmount,
        OrderStatus status = OrderStatus.Completed) =>
        new()
        {
            Id = id,
            CustomerId = customer.Id,
            Customer = customer,
            OrderDate = orderDate,
            Status = status,
            TotalAmount = totalAmount,
            CreatedAt = orderDate
        };

    private static OrderItem CreateOrderItem(
        long id,
        Order order,
        Product product,
        int quantity,
        decimal unitPrice) =>
        new()
        {
            Id = id,
            OrderId = order.Id,
            Order = order,
            ProductId = product.Id,
            Product = product,
            Quantity = quantity,
            UnitPrice = unitPrice,
            LineTotal = quantity * unitPrice
        };

    private static ReturnEntity CreateReturn(
        long id,
        OrderItem orderItem,
        int quantity,
        decimal amount,
        string reason,
        DateTime returnDate) =>
        new()
        {
            Id = id,
            OrderItemId = orderItem.Id,
            OrderItem = orderItem,
            Quantity = quantity,
            Amount = amount,
            Reason = reason,
            ReturnDate = returnDate
        };

    private sealed record QueryCase(
        string Question,
        AiQuestionUnderstanding Understanding,
        AiAnalysisType Analysis,
        DateTimeOffset ExpectedFromUtc,
        TopProductsMetric? ExpectedMetric,
        int? ExpectedLimit,
        string ExpectedOperation);

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }

    private sealed class RecordingAnalyticsService(IAnalyticsService inner)
        : IAnalyticsService
    {
        public List<string> Calls { get; } = [];

        public void Clear() => Calls.Clear();

        public AnalyticsDateRange CreateDateRange(DateTimeOffset from, DateTimeOffset to) =>
            inner.CreateDateRange(from, to);

        public Task<SalesSummaryResponse> GetSalesSummaryAsync(
            DateTimeOffset from,
            DateTimeOffset to,
            CancellationToken cancellationToken = default)
        {
            Calls.Add("sales-summary");
            return inner.GetSalesSummaryAsync(from, to, cancellationToken);
        }

        public Task<SalesTrendResponse> GetSalesTrendAsync(
            DateTimeOffset from,
            DateTimeOffset to,
            AnalyticsGranularity granularity,
            CancellationToken cancellationToken = default)
        {
            Calls.Add("sales-trend");
            return inner.GetSalesTrendAsync(from, to, granularity, cancellationToken);
        }

        public Task<TopProductsResponse> GetTopProductsAsync(
            DateTimeOffset from,
            DateTimeOffset to,
            TopProductsMetric metric,
            int? limit = null,
            long? categoryId = null,
            CancellationToken cancellationToken = default)
        {
            Calls.Add("top-products");
            return inner.GetTopProductsAsync(
                from,
                to,
                metric,
                limit,
                categoryId,
                cancellationToken);
        }

        public Task<CategoryPerformanceResponse> GetCategoryPerformanceAsync(
            DateTimeOffset from,
            DateTimeOffset to,
            CancellationToken cancellationToken = default)
        {
            Calls.Add("category-performance");
            return inner.GetCategoryPerformanceAsync(from, to, cancellationToken);
        }

        public Task<ReturnAnalyticsResponse> GetReturnAnalyticsAsync(
            DateTimeOffset from,
            DateTimeOffset to,
            CancellationToken cancellationToken = default)
        {
            Calls.Add("return-analysis");
            return inner.GetReturnAnalyticsAsync(from, to, cancellationToken);
        }
    }
}
