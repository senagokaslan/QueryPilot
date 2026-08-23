using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using QueryPilot.Api.Common.ErrorHandling;
using QueryPilot.Api.Common.Exceptions;
using QueryPilot.Api.Features.Ai;
using QueryPilot.Api.Features.Analytics;
using Xunit;

namespace QueryPilot.Api.IntegrationTests;

[Collection(PostgreSqlIntegrationCollection.Name)]
public sealed class ApiHttpContractTests(PostgreSqlDatabaseFixture database)
{
    private static readonly WebApplicationFactoryClientOptions ClientOptions = new()
    {
        AllowAutoRedirect = false,
        BaseAddress = new Uri("https://localhost")
    };

    [PostgreSqlFact]
    public async Task Category_product_order_return_and_analytics_endpoints_work_over_http()
    {
        using var factory = new QueryPilotApiFactory(database);
        using var client = factory.CreateClient(ClientOptions);
        var suffix = Guid.NewGuid().ToString("N");

        using var categoryResponse = await client.PostAsJsonAsync(
            "/api/categories",
            new { Name = $"HTTP-{suffix}" });
        Assert.Equal(HttpStatusCode.Created, categoryResponse.StatusCode);
        var categoryId = await ReadInt64Async(categoryResponse, "id");

        using var categoryGet = await client.GetAsync($"/api/categories/{categoryId}");
        Assert.Equal(HttpStatusCode.OK, categoryGet.StatusCode);

        using var productResponse = await client.PostAsJsonAsync(
            "/api/products",
            new
            {
                Name = $"HTTP Product {suffix}",
                SKU = $"HTTP-{suffix}",
                CategoryId = categoryId,
                UnitPrice = 25.50m,
                IsActive = true
            });
        Assert.Equal(HttpStatusCode.Created, productResponse.StatusCode);
        var productId = await ReadInt64Async(productResponse, "id");

        using var productGet = await client.GetAsync($"/api/products/{productId}");
        Assert.Equal(HttpStatusCode.OK, productGet.StatusCode);

        using var orderResponse = await client.PostAsJsonAsync(
            "/api/orders",
            new
            {
                CustomerId = 1,
                Items = new[] { new { ProductId = productId, Quantity = 2 } }
            });
        Assert.Equal(HttpStatusCode.Created, orderResponse.StatusCode);
        using var orderJson = JsonDocument.Parse(
            await orderResponse.Content.ReadAsStringAsync());
        var orderId = orderJson.RootElement.GetProperty("id").GetInt64();
        Assert.Equal(51m, orderJson.RootElement.GetProperty("totalAmount").GetDecimal());

        using var orderGet = await client.GetAsync($"/api/orders/{orderId}");
        Assert.Equal(HttpStatusCode.OK, orderGet.StatusCode);

        using var returnResponse = await client.PostAsJsonAsync(
            "/api/returns",
            new
            {
                OrderItemId = 3,
                Quantity = 1,
                Reason = "HTTP contract test"
            });
        Assert.Equal(HttpStatusCode.Created, returnResponse.StatusCode);
        using var returnJson = JsonDocument.Parse(
            await returnResponse.Content.ReadAsStringAsync());
        Assert.Equal(10m, returnJson.RootElement.GetProperty("amount").GetDecimal());

        const string range = "from=2024-12-30T00:00:00Z&to=2025-01-06T00:00:00Z";
        using var analyticsResponse = await client.GetAsync(
            $"/api/analytics/summary?{range}");
        Assert.Equal(HttpStatusCode.OK, analyticsResponse.StatusCode);
        using var analyticsJson = JsonDocument.Parse(
            await analyticsResponse.Content.ReadAsStringAsync());
        Assert.Equal(
            400m,
            analyticsJson.RootElement.GetProperty("totalRevenue").GetDecimal());
    }

    [PostgreSqlFact]
    public async Task Fake_ai_provider_routes_all_supported_intents_over_http()
    {
        var understandings = CreateSupportedUnderstandings();
        var fakeAi = new FakeAiService(
            (question, _) => Task.FromResult(understandings[question]),
            (_, _, _) => Task.FromResult(new AiResultExplanation("Sonuç hazır.")));
        using var baseFactory = new QueryPilotApiFactory(database);
        using var factory = baseFactory.WithTestServices(services =>
        {
            services.RemoveAll<IAiService>();
            services.AddSingleton<IAiService>(fakeAi);
        });
        using var client = factory.CreateClient(ClientOptions);

        foreach (var (question, understanding) in understandings)
        {
            using var response = await client.PostAsJsonAsync(
                "/api/ai/query",
                new { Question = question });

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var result = await response.Content
                .ReadFromJsonAsync<AiAnalyticsExecutionResult>();
            Assert.NotNull(result);
            Assert.Equal(AiAnalyticsExecutionStatus.Completed, result.Status);
            Assert.Equal(understanding.Analysis, result.Intent!.Analysis);
            Assert.NotNull(result.Data);
            if (understanding.Analysis == AiAnalysisType.TopProducts)
            {
                Assert.Equal(
                    AnalyticsService.DefaultTopProductsLimit,
                    result.Intent.Limit);
            }
        }
    }

    [PostgreSqlFact]
    public async Task Http_pipeline_returns_common_400_404_and_409_problem_contracts()
    {
        using var factory = new QueryPilotApiFactory(database);
        using var client = factory.CreateClient(ClientOptions);

        using var badRequest = await client.GetAsync("/api/analytics/summary");
        await AssertProblemAsync(
            badRequest,
            HttpStatusCode.BadRequest,
            ProblemDetailsTypes.BadRequest,
            "Validation failed.");

        using var notFound = await client.GetAsync("/api/categories/9223372036854775807");
        await AssertProblemAsync(
            notFound,
            HttpStatusCode.NotFound,
            ProblemDetailsTypes.NotFound,
            "Resource not found.");

        using var conflict = await client.PostAsJsonAsync(
            "/api/categories",
            new { Name = "Electronics" });
        await AssertProblemAsync(
            conflict,
            HttpStatusCode.Conflict,
            ProblemDetailsTypes.Conflict,
            "The request conflicts with the current state.");
    }

    [PostgreSqlFact]
    public async Task Http_pipeline_returns_safe_500_and_503_problem_contracts()
    {
        using var baseFactory = new QueryPilotApiFactory(database);
        using var internalErrorFactory = CreateThrowingCoordinatorFactory(
            baseFactory,
            new InvalidOperationException("Sensitive internal detail."));
        using var internalErrorClient = internalErrorFactory.CreateClient(ClientOptions);
        using var internalError = await internalErrorClient.PostAsJsonAsync(
            "/api/ai/query",
            new { Question = "Bu ay satışlarımız nasıl?" });
        await AssertProblemAsync(
            internalError,
            HttpStatusCode.InternalServerError,
            ProblemDetailsTypes.InternalServerError,
            "An unexpected error occurred.");
        Assert.DoesNotContain(
            "Sensitive",
            await internalError.Content.ReadAsStringAsync(),
            StringComparison.OrdinalIgnoreCase);

        using var unavailableFactory = CreateThrowingCoordinatorFactory(
            baseFactory,
            new AiServiceUnavailableException("Provider key and response detail."));
        using var unavailableClient = unavailableFactory.CreateClient(ClientOptions);
        using var unavailable = await unavailableClient.PostAsJsonAsync(
            "/api/ai/query",
            new { Question = "Bu ay satışlarımız nasıl?" });
        await AssertProblemAsync(
            unavailable,
            HttpStatusCode.ServiceUnavailable,
            ProblemDetailsTypes.ServiceUnavailable,
            "AI service is temporarily unavailable.");
        Assert.DoesNotContain(
            "Provider key",
            await unavailable.Content.ReadAsStringAsync(),
            StringComparison.OrdinalIgnoreCase);
    }

    private static WebApplicationFactory<Program> CreateThrowingCoordinatorFactory(
        QueryPilotApiFactory factory,
        Exception exception) =>
        factory.WithTestServices(services =>
        {
            services.RemoveAll<IAiAnalyticsCoordinator>();
            services.AddSingleton<IAiAnalyticsCoordinator>(
                new ThrowingCoordinator(exception));
        });

    private static Dictionary<string, AiQuestionUnderstanding>
        CreateSupportedUnderstandings()
    {
        const string from = "2024-12-30T00:00:00Z";
        const string to = "2025-01-06T00:00:00Z";
        return new Dictionary<string, AiQuestionUnderstanding>
        {
            ["summary"] = CreateUnderstanding(AiAnalysisType.SalesSummary, from, to),
            ["trend"] = CreateUnderstanding(
                AiAnalysisType.SalesTrend,
                from,
                to,
                granularity: AiGranularity.Daily),
            ["products"] = CreateUnderstanding(
                AiAnalysisType.TopProducts,
                from,
                to,
                metric: AiTopProductsMetric.Quantity),
            ["categories"] = CreateUnderstanding(
                AiAnalysisType.CategoryPerformance,
                from,
                to),
            ["returns"] = CreateUnderstanding(AiAnalysisType.ReturnAnalysis, from, to)
        };
    }

    private static AiQuestionUnderstanding CreateUnderstanding(
        AiAnalysisType analysis,
        string from,
        string to,
        AiGranularity? granularity = null,
        AiTopProductsMetric? metric = null) =>
        new(
            analysis,
            Period: null,
            From: from,
            To: to,
            ProductName: null,
            CategoryName: null,
            Granularity: granularity,
            Metric: metric,
            Limit: null);

    private static async Task<long> ReadInt64Async(
        HttpResponseMessage response,
        string propertyName)
    {
        using var document = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync());
        return document.RootElement.GetProperty(propertyName).GetInt64();
    }

    private static async Task AssertProblemAsync(
        HttpResponseMessage response,
        HttpStatusCode expectedStatus,
        string expectedType,
        string expectedTitle)
    {
        Assert.Equal(expectedStatus, response.StatusCode);
        Assert.Equal(
            "application/problem+json",
            response.Content.Headers.ContentType?.MediaType);
        using var document = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync());
        var root = document.RootElement;
        Assert.Equal((int)expectedStatus, root.GetProperty("status").GetInt32());
        Assert.Equal(expectedType, root.GetProperty("type").GetString());
        Assert.Equal(expectedTitle, root.GetProperty("title").GetString());
        Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("instance").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("traceId").GetString()));
    }

    private sealed class ThrowingCoordinator(Exception exception)
        : IAiAnalyticsCoordinator
    {
        public Task<AiAnalyticsExecutionResult> ExecuteAsync(
            string question,
            CancellationToken cancellationToken = default) =>
            Task.FromException<AiAnalyticsExecutionResult>(exception);
    }
}
