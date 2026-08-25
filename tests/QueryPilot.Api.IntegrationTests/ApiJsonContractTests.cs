using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using QueryPilot.Api.Common.ErrorHandling;
using QueryPilot.Api.Features.Ai;
using Xunit;

namespace QueryPilot.Api.IntegrationTests;

[Collection(PostgreSqlIntegrationCollection.Name)]
public sealed class ApiJsonContractTests(PostgreSqlDatabaseFixture database)
{
    private static readonly WebApplicationFactoryClientOptions ClientOptions = new()
    {
        AllowAutoRedirect = false,
        BaseAddress = new Uri("https://localhost")
    };

    [PostgreSqlFact]
    public async Task Success_contracts_use_camel_case_utc_dates_and_numeric_values()
    {
        using var factory = new QueryPilotApiFactory(database);
        using var client = factory.CreateClient(ClientOptions);

        using var categories = await GetJsonAsync(
            client,
            "/api/categories?page=1&pageSize=1");
        AssertCamelCaseProperties(categories.RootElement);
        AssertPagedShape(categories.RootElement, expectedPageSize: 1);
        AssertUtcIso8601(
            categories.RootElement.GetProperty("items")[0].GetProperty("createdAt"));

        using var products = await GetJsonAsync(
            client,
            "/api/products?page=1&pageSize=1");
        AssertCamelCaseProperties(products.RootElement);
        AssertPagedShape(products.RootElement, expectedPageSize: 1);
        Assert.Equal(
            JsonValueKind.Number,
            products.RootElement.GetProperty("items")[0]
                .GetProperty("unitPrice").ValueKind);

        using var customers = await GetJsonAsync(
            client,
            "/api/customers?page=1&pageSize=1");
        AssertPagedShape(customers.RootElement, expectedPageSize: 1);

        using var orders = await GetJsonAsync(
            client,
            "/api/orders?page=1&pageSize=1");
        AssertPagedShape(orders.RootElement, expectedPageSize: 1);

        const string range =
            "from=2024-12-30T00:00:00Z&to=2025-01-06T00:00:00Z";
        using var summary = await GetJsonAsync(
            client,
            $"/api/analytics/summary?{range}");
        AssertCamelCaseProperties(summary.RootElement);
        AssertUtcIso8601(summary.RootElement.GetProperty("fromUtc"));
        AssertUtcIso8601(summary.RootElement.GetProperty("toUtc"));
        Assert.Equal(
            JsonValueKind.Number,
            summary.RootElement.GetProperty("totalRevenue").ValueKind);

        using var trend = await GetJsonAsync(
            client,
            $"/api/analytics/sales-trend?{range}&granularity=Daily");
        AssertCamelCaseProperties(trend.RootElement);
        var point = trend.RootElement.GetProperty("revenue")[0];
        Assert.Equal(
            ["periodStart", "label", "value"],
            point.EnumerateObject().Select(property => property.Name).ToArray());
        AssertUtcIso8601(point.GetProperty("periodStart"));
        Assert.Equal(JsonValueKind.String, point.GetProperty("label").ValueKind);
        Assert.Equal(JsonValueKind.Number, point.GetProperty("value").ValueKind);
    }

    [PostgreSqlFact]
    public async Task Ai_contract_keeps_structured_data_and_explanation_separate()
    {
        var fakeAi = new FakeAiService(
            new AiQuestionUnderstanding(
                AiAnalysisType.SalesSummary,
                Period: null,
                From: "2024-12-30T00:00:00Z",
                To: "2025-01-06T00:00:00Z",
                ProductName: null,
                CategoryName: null,
                Granularity: null,
                Metric: null,
                Limit: null),
            new AiResultExplanation("Sonuç hazır."));
        using var baseFactory = new QueryPilotApiFactory(database);
        using var factory = baseFactory.WithTestServices(services =>
        {
            services.RemoveAll<IAiService>();
            services.AddSingleton<IAiService>(fakeAi);
        });
        using var client = factory.CreateClient(ClientOptions);

        using var response = await client.PostAsJsonAsync(
            "/api/ai/query",
            new { question = "Özet" });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync());
        var root = json.RootElement;

        AssertCamelCaseProperties(root);
        Assert.Equal(JsonValueKind.Object, root.GetProperty("data").ValueKind);
        Assert.Equal(
            JsonValueKind.Object,
            root.GetProperty("explanation").ValueKind);
        Assert.False(root.GetProperty("data").TryGetProperty("explanation", out _));
        Assert.Equal(
            "Sonuç hazır.",
            root.GetProperty("explanation").GetProperty("text").GetString());
    }

    [PostgreSqlFact]
    public async Task Requests_beyond_pagination_and_analytics_limits_return_problem_details()
    {
        using var factory = new QueryPilotApiFactory(database);
        using var client = factory.CreateClient(ClientOptions);

        using var pageSize = await client.GetAsync(
            "/api/categories?page=1&pageSize=101");
        await AssertValidationProblemAsync(pageSize, "pageSize");

        const string range =
            "from=2024-12-30T00:00:00Z&to=2025-01-06T00:00:00Z";
        using var topProducts = await client.GetAsync(
            $"/api/analytics/top-products?{range}&metric=Quantity&limit=51");
        await AssertValidationProblemAsync(topProducts, "limit");

        using var excessiveRange = await client.GetAsync(
            "/api/analytics/summary?from=2020-01-01T00:00:00Z&to=2026-01-02T00:00:00Z");
        await AssertValidationProblemAsync(excessiveRange, "to");
    }

    [PostgreSqlFact]
    public async Task Empty_framework_errors_also_use_problem_details()
    {
        using var factory = new QueryPilotApiFactory(database);
        using var client = factory.CreateClient(ClientOptions);

        using var notFound = await client.GetAsync("/api/does-not-exist");
        await AssertProblemAsync(
            notFound,
            HttpStatusCode.NotFound,
            ProblemDetailsTypes.NotFound);

        using var methodNotAllowed = await client.PatchAsync(
            "/api/categories",
            content: null);
        await AssertProblemAsync(
            methodNotAllowed,
            HttpStatusCode.MethodNotAllowed,
            ProblemDetailsTypes.MethodNotAllowed);

        using var unsupportedMediaType = await client.PostAsync(
            "/api/categories",
            content: null);
        await AssertProblemAsync(
            unsupportedMediaType,
            HttpStatusCode.UnsupportedMediaType,
            ProblemDetailsTypes.UnsupportedMediaType);
    }

    private static async Task<JsonDocument> GetJsonAsync(
        HttpClient client,
        string requestUri)
    {
        using var response = await client.GetAsync(requestUri);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(
            "application/json",
            response.Content.Headers.ContentType?.MediaType);
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync());
    }

    private static void AssertPagedShape(
        JsonElement root,
        int expectedPageSize)
    {
        Assert.Equal(JsonValueKind.Array, root.GetProperty("items").ValueKind);
        Assert.Equal(1, root.GetProperty("page").GetInt32());
        Assert.Equal(expectedPageSize, root.GetProperty("pageSize").GetInt32());
        Assert.Equal(JsonValueKind.Number, root.GetProperty("totalCount").ValueKind);
        Assert.Equal(JsonValueKind.Number, root.GetProperty("totalPages").ValueKind);
    }

    private static void AssertUtcIso8601(JsonElement value)
    {
        Assert.Equal(JsonValueKind.String, value.ValueKind);
        var text = value.GetString();
        Assert.NotNull(text);
        Assert.True(DateTimeOffset.TryParse(text, out var parsed));
        Assert.Equal(TimeSpan.Zero, parsed.Offset);
    }

    private static void AssertCamelCaseProperties(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                AssertCamelCaseProperties(item);
            }

            return;
        }

        if (element.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        foreach (var property in element.EnumerateObject())
        {
            Assert.True(
                property.Name.Length == 0 || !char.IsUpper(property.Name[0]),
                $"JSON property '{property.Name}' must use camelCase.");
            AssertCamelCaseProperties(property.Value);
        }
    }

    private static async Task AssertValidationProblemAsync(
        HttpResponseMessage response,
        string expectedErrorKey)
    {
        await AssertProblemAsync(
            response,
            HttpStatusCode.BadRequest,
            ProblemDetailsTypes.BadRequest);
        using var document = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync());
        Assert.True(
            document.RootElement.GetProperty("errors")
                .TryGetProperty(expectedErrorKey, out _));
    }

    private static async Task AssertProblemAsync(
        HttpResponseMessage response,
        HttpStatusCode statusCode,
        string expectedType)
    {
        Assert.Equal(statusCode, response.StatusCode);
        Assert.Equal(
            "application/problem+json",
            response.Content.Headers.ContentType?.MediaType);
        using var document = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync());
        var root = document.RootElement;
        AssertCamelCaseProperties(root);
        Assert.Equal((int)statusCode, root.GetProperty("status").GetInt32());
        Assert.Equal(expectedType, root.GetProperty("type").GetString());
        Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("instance").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("traceId").GetString()));
    }
}
