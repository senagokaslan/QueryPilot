using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace QueryPilot.Api.IntegrationTests;

[Collection(PostgreSqlIntegrationCollection.Name)]
public sealed class SwaggerAndCorsHttpTests(PostgreSqlDatabaseFixture database)
{
    private const string AllowedOrigin = "https://frontend.example.com";
    private const string DisallowedOrigin = "https://attacker.example.com";

    private static readonly WebApplicationFactoryClientOptions ClientOptions = new()
    {
        AllowAutoRedirect = false,
        BaseAddress = new Uri("https://localhost")
    };

    [PostgreSqlFact]
    public async Task Swagger_describes_project_tags_contracts_and_string_enums()
    {
        using var factory = new QueryPilotApiFactory(
            database,
            settings: new Dictionary<string, string?>
            {
                ["Swagger:Enabled"] = "true"
            });
        using var client = factory.CreateClient(ClientOptions);

        using var response = await client.GetAsync("/swagger/v1/swagger.json");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync());
        var root = document.RootElement;
        var info = root.GetProperty("info");
        Assert.Equal("QueryPilot API", info.GetProperty("title").GetString());
        Assert.Equal("v1", info.GetProperty("version").GetString());
        Assert.Contains(
            "contract only",
            info.GetProperty("description").GetString(),
            StringComparison.OrdinalIgnoreCase);

        var paths = root.GetProperty("paths");
        AssertOperationTag(paths, "/api/categories", "get", "Categories");
        AssertOperationTag(paths, "/api/products", "get", "Products");
        AssertOperationTag(paths, "/api/customers", "get", "Customers");
        AssertOperationTag(paths, "/api/orders", "get", "Orders");
        AssertOperationTag(paths, "/api/returns", "post", "Returns");
        AssertOperationTag(paths, "/api/analytics/summary", "get", "Analytics");
        AssertOperationTag(paths, "/api/ai/query", "post", "AI");

        var categoryResponses = paths.GetProperty("/api/categories")
            .GetProperty("post")
            .GetProperty("responses");
        AssertResponseSchema(categoryResponses, "201", "CategoryResponse");
        AssertProblemSchema(categoryResponses, "400", "ProblemDetails");
        AssertProblemSchema(categoryResponses, "409", "ProblemDetails");

        var aiPost = paths.GetProperty("/api/ai/query").GetProperty("post");
        var aiRequestSchema = aiPost.GetProperty("requestBody")
            .GetProperty("content")
            .GetProperty("application/json")
            .GetProperty("schema")
            .GetProperty("$ref")
            .GetString();
        Assert.EndsWith("/AiQueryRequest", aiRequestSchema, StringComparison.Ordinal);
        var aiResponses = aiPost.GetProperty("responses");
        AssertResponseSchema(aiResponses, "200", "AiAnalyticsExecutionResult");
        AssertProblemSchema(aiResponses, "400", "HttpValidationProblemDetails");
        AssertProblemSchema(aiResponses, "503", "ProblemDetails");
        AssertProblemSchema(aiResponses, "500", "ProblemDetails");

        var schemas = root.GetProperty("components").GetProperty("schemas");
        AssertStringEnum(schemas, "OrderStatus", "Pending", "Completed", "Cancelled");
        AssertStringEnum(schemas, "AnalyticsGranularity", "Daily", "Weekly", "Monthly");
        AssertStringEnum(schemas, "TopProductsMetric", "Quantity", "Revenue");
    }

    [PostgreSqlFact]
    public async Task Cors_preflight_allows_configured_origin_and_rejects_other_origins()
    {
        using var factory = new QueryPilotApiFactory(
            database,
            settings: new Dictionary<string, string?>
            {
                ["Cors:AllowedOrigins:0"] = AllowedOrigin,
                ["Cors:AllowCredentials"] = "true"
            });
        using var client = factory.CreateClient(ClientOptions);

        using var allowed = await SendPreflightAsync(client, AllowedOrigin);

        Assert.Equal(HttpStatusCode.NoContent, allowed.StatusCode);
        Assert.Equal(
            AllowedOrigin,
            Assert.Single(allowed.Headers.GetValues("Access-Control-Allow-Origin")));
        Assert.Equal(
            "true",
            Assert.Single(allowed.Headers.GetValues("Access-Control-Allow-Credentials")));
        Assert.Contains(
            "GET",
            Assert.Single(allowed.Headers.GetValues("Access-Control-Allow-Methods")));

        using var disallowed = await SendPreflightAsync(client, DisallowedOrigin);

        Assert.Equal(HttpStatusCode.NoContent, disallowed.StatusCode);
        Assert.False(disallowed.Headers.Contains("Access-Control-Allow-Origin"));
        Assert.False(disallowed.Headers.Contains("Access-Control-Allow-Credentials"));
    }

    [PostgreSqlFact]
    public async Task Production_keeps_swagger_disabled_and_allows_only_exact_https_origin()
    {
        using var factory = new QueryPilotApiFactory(
            database,
            environment: "Production",
            settings: new Dictionary<string, string?>
            {
                ["Swagger:Enabled"] = "true",
                ["Cors:AllowedOrigins:0"] = AllowedOrigin,
                ["Cors:AllowCredentials"] = "true"
            });
        using var client = factory.CreateClient(ClientOptions);

        using var swagger = await client.GetAsync("/swagger/v1/swagger.json");
        Assert.Equal(HttpStatusCode.NotFound, swagger.StatusCode);

        using var exactOrigin = await SendPreflightAsync(client, AllowedOrigin);
        Assert.Equal(
            AllowedOrigin,
            Assert.Single(exactOrigin.Headers.GetValues("Access-Control-Allow-Origin")));

        using var differentOrigin = await SendPreflightAsync(client, DisallowedOrigin);
        Assert.False(differentOrigin.Headers.Contains("Access-Control-Allow-Origin"));
    }

    private static async Task<HttpResponseMessage> SendPreflightAsync(
        HttpClient client,
        string origin)
    {
        using var request = new HttpRequestMessage(HttpMethod.Options, "/api/categories");
        request.Headers.Add("Origin", origin);
        request.Headers.Add("Access-Control-Request-Method", "GET");
        request.Headers.Add("Access-Control-Request-Headers", "content-type");
        return await client.SendAsync(request);
    }

    private static void AssertOperationTag(
        JsonElement paths,
        string path,
        string method,
        string expectedTag)
    {
        var tags = paths.GetProperty(path)
            .GetProperty(method)
            .GetProperty("tags");
        Assert.Equal(expectedTag, Assert.Single(tags.EnumerateArray()).GetString());
    }

    private static void AssertStringEnum(
        JsonElement schemas,
        string schemaName,
        params string[] expectedValues)
    {
        var schema = schemas.GetProperty(schemaName);
        Assert.Equal("string", schema.GetProperty("type").GetString());
        Assert.Equal(
            expectedValues,
            schema.GetProperty("enum")
                .EnumerateArray()
                .Select(value => value.GetString())
                .ToArray());
    }

    private static void AssertProblemSchema(
        JsonElement responses,
        string statusCode,
        string expectedSchema) =>
        AssertResponseSchema(
            responses,
            statusCode,
            expectedSchema,
            "application/problem+json");

    private static void AssertResponseSchema(
        JsonElement responses,
        string statusCode,
        string expectedSchema,
        string mediaType = "application/json")
    {
        var schemaReference = responses.GetProperty(statusCode)
            .GetProperty("content")
            .GetProperty(mediaType)
            .GetProperty("schema")
            .GetProperty("$ref")
            .GetString();
        Assert.EndsWith($"/{expectedSchema}", schemaReference, StringComparison.Ordinal);
    }
}
