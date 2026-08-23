using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using QueryPilot.Api.Features.Ai;
using QueryPilot.Api.Features.Analytics;
using QueryPilot.Api.Features.Analytics.Dtos;
using QueryPilot.Api.Features.Categories;
using QueryPilot.Api.Features.Customers;
using QueryPilot.Api.Features.Orders;
using QueryPilot.Api.Features.Products;
using Xunit;

namespace QueryPilot.Api.IntegrationTests;

[Collection(PostgreSqlIntegrationCollection.Name)]
public sealed class DynamicAnalyticsDemoTests(PostgreSqlDatabaseFixture database)
{
    private const string From = "2042-01-01T00:00:00Z";
    private const string To = "2042-02-01T00:00:00Z";
    private const string Question = "2042 Ocak ayında en çok satan 5 ürün hangileri?";

    private static readonly WebApplicationFactoryClientOptions ClientOptions = new()
    {
        AllowAutoRedirect = false,
        BaseAddress = new Uri("https://localhost")
    };

    [PostgreSqlFact]
    public async Task New_completed_orders_change_live_analytics_and_ai_explanation()
    {
        var demo = await SeedBaselineAsync();
        var explanationPayloads = new List<string>();
        var aiService = new FakeAiService(
            (_, _) => Task.FromResult(new AiQuestionUnderstanding(
                AiAnalysisType.TopProducts,
                Period: null,
                From,
                To,
                ProductName: null,
                CategoryName: null,
                Granularity: null,
                Metric: AiTopProductsMetric.Quantity,
                Limit: 5)),
            (_, analyticsJson, _) =>
            {
                explanationPayloads.Add(analyticsJson);
                using var document = JsonDocument.Parse(analyticsJson);
                var leaderQuantity = document.RootElement
                    .GetProperty("items")[0]
                    .GetProperty("quantity")
                    .GetInt64();
                return Task.FromResult(new AiResultExplanation(
                    $"Güncel lider {leaderQuantity} adet satışa sahip."));
            });
        using var baseFactory = new QueryPilotApiFactory(database);
        using var factory = baseFactory.WithTestServices(services =>
        {
            services.RemoveAll<IAiService>();
            services.AddSingleton<IAiService>(aiService);
        });
        using var client = factory.CreateClient(ClientOptions);

        var baselineTopProducts = await GetTopProductsAsync(client);
        var baselineSummary = await GetSummaryAsync(client);
        var baselineCategories = await GetCategoriesAsync(client);
        var baselineAi = await AskAiAsync(client);

        Assert.Equal(demo.BaselineLeaderName, baselineTopProducts.Items[0].Name);
        Assert.Equal(
            baselineTopProducts.Items.Sum(item => item.Revenue),
            baselineSummary.TotalRevenue);
        Assert.Equal(baselineTopProducts.Items[0].Quantity, baselineAi.Data
            .GetProperty("items")[0]
            .GetProperty("quantity").GetInt64());
        Assert.Contains(
            $"{baselineTopProducts.Items[0].Quantity} adet",
            baselineAi.ExplanationText);

        var baselineTargetProduct = baselineTopProducts.Items.Single(
            item => item.ProductId == demo.TargetProductId);
        var growthDelta = await AddCompletedGrowthOrdersAsync(demo);
        var expectedTargetQuantity = baselineTargetProduct.Quantity + growthDelta.Quantity;
        var expectedTargetRevenue = baselineTargetProduct.Revenue + growthDelta.Revenue;
        var expectedTotalRevenue = baselineSummary.TotalRevenue + growthDelta.Revenue;

        var changedTopProducts = await GetTopProductsAsync(client);
        var changedSummary = await GetSummaryAsync(client);
        var changedCategories = await GetCategoriesAsync(client);

        Assert.Equal(demo.TargetProductName, changedTopProducts.Items[0].Name);
        Assert.Equal(expectedTargetQuantity, changedTopProducts.Items[0].Quantity);
        Assert.Equal(expectedTargetRevenue, changedTopProducts.Items[0].Revenue);
        Assert.Equal(growthDelta.Revenue, changedSummary.TotalRevenue - baselineSummary.TotalRevenue);
        Assert.Equal(expectedTotalRevenue, changedSummary.TotalRevenue);

        var baselineTargetCategory = baselineCategories.Items.Single(
            item => item.CategoryId == demo.TargetCategoryId);
        var changedTargetCategory = changedCategories.Items.Single(
            item => item.CategoryId == demo.TargetCategoryId);
        Assert.Equal(
            baselineTargetCategory.Revenue / baselineSummary.TotalRevenue * 100m,
            baselineTargetCategory.RevenueSharePercentage);
        Assert.Equal(
            baselineTargetCategory.Revenue + growthDelta.Revenue,
            changedTargetCategory.Revenue);
        Assert.Equal(
            changedTargetCategory.Revenue / changedSummary.TotalRevenue * 100m,
            changedTargetCategory.RevenueSharePercentage);

        await AddCancelledOrderAsync(demo);
        using var invalidOrder = await client.PostAsJsonAsync(
            "/api/orders",
            new
            {
                CustomerId = demo.CustomerId,
                Items = new[]
                {
                    new { ProductId = long.MaxValue, Quantity = 1 }
                }
            });
        Assert.Equal(HttpStatusCode.NotFound, invalidOrder.StatusCode);

        var controlTopProducts = await GetTopProductsAsync(client);
        var controlSummary = await GetSummaryAsync(client);
        var controlCategories = await GetCategoriesAsync(client);
        Assert.Equivalent(changedTopProducts, controlTopProducts, strict: true);
        Assert.Equivalent(changedSummary, controlSummary, strict: true);
        Assert.Equivalent(changedCategories, controlCategories, strict: true);

        var currentAi = await AskAiAsync(client);
        Assert.Equal(2, explanationPayloads.Count);
        Assert.NotEqual(explanationPayloads[0], explanationPayloads[1]);
        Assert.Contains(demo.BaselineLeaderName, explanationPayloads[0]);
        Assert.Contains(demo.TargetProductName, explanationPayloads[1]);
        Assert.Equal(demo.TargetProductName, currentAi.Data.GetProperty("items")[0]
            .GetProperty("name").GetString());
        Assert.Equal(expectedTargetQuantity, currentAi.Data.GetProperty("items")[0]
            .GetProperty("quantity").GetInt64());
        Assert.Contains($"{expectedTargetQuantity} adet", currentAi.ExplanationText);
    }

    private async Task<DemoFixture> SeedBaselineAsync()
    {
        await using var dbContext = database.CreateContext();
        var suffix = Guid.NewGuid().ToString("N");
        var createdAt = new DateTime(2041, 12, 1, 0, 0, 0, DateTimeKind.Utc);
        var leaderCategory = new Category
        {
            Name = $"Demo Leader {suffix}",
            IsActive = true,
            CreatedAt = createdAt
        };
        var targetCategory = new Category
        {
            Name = $"Demo Growth {suffix}",
            IsActive = true,
            CreatedAt = createdAt
        };
        var leader = new Product
        {
            Name = $"Baseline Product {suffix}",
            SKU = $"BASE-{suffix}",
            Category = leaderCategory,
            UnitPrice = 100m,
            IsActive = true,
            CreatedAt = createdAt
        };
        var target = new Product
        {
            Name = $"Growth Product {suffix}",
            SKU = $"GROW-{suffix}",
            Category = targetCategory,
            UnitPrice = 200m,
            IsActive = true,
            CreatedAt = createdAt
        };
        var customer = new Customer
        {
            Name = "CV Demo Customer",
            Email = $"cv-demo-{suffix}@example.com",
            NormalizedEmail = $"cv-demo-{suffix}@example.com",
            City = "Istanbul",
            IsActive = true,
            CreatedAt = createdAt
        };
        dbContext.AddRange(leaderCategory, targetCategory, leader, target, customer);
        await dbContext.SaveChangesAsync();

        var baselineOrder = CreateOrder(
            customer.Id,
            new DateTime(2042, 1, 5, 12, 0, 0, DateTimeKind.Utc),
            OrderStatus.Completed,
            (leader.Id, 10, 100m),
            (target.Id, 2, 200m));
        dbContext.Orders.Add(baselineOrder);
        await dbContext.SaveChangesAsync();

        return new DemoFixture(
            customer.Id,
            leader.Name,
            target.Id,
            target.Name,
            targetCategory.Id);
    }

    private async Task<GrowthDelta> AddCompletedGrowthOrdersAsync(DemoFixture demo)
    {
        await using var dbContext = database.CreateContext();
        var activeTarget = await dbContext.Products
            .AsNoTracking()
            .SingleAsync(product => product.Id == demo.TargetProductId && product.IsActive);
        var growthOrders = new[]
        {
            CreateOrder(
                demo.CustomerId,
                new DateTime(2042, 1, 10, 12, 0, 0, DateTimeKind.Utc),
                OrderStatus.Completed,
                (activeTarget.Id, 10, activeTarget.UnitPrice)),
            CreateOrder(
                demo.CustomerId,
                new DateTime(2042, 1, 20, 12, 0, 0, DateTimeKind.Utc),
                OrderStatus.Completed,
                (activeTarget.Id, 10, activeTarget.UnitPrice))
        };
        var delta = new GrowthDelta(
            growthOrders.SelectMany(order => order.Items).Sum(item => (long)item.Quantity),
            growthOrders.Sum(order => order.TotalAmount));

        dbContext.Orders.AddRange(growthOrders);
        await dbContext.SaveChangesAsync();
        return delta;
    }

    private async Task AddCancelledOrderAsync(DemoFixture demo)
    {
        await using var dbContext = database.CreateContext();
        dbContext.Orders.Add(CreateOrder(
            demo.CustomerId,
            new DateTime(2042, 1, 25, 12, 0, 0, DateTimeKind.Utc),
            OrderStatus.Cancelled,
            (demo.TargetProductId, 1_000, 200m)));
        await dbContext.SaveChangesAsync();
    }

    private static Order CreateOrder(
        long customerId,
        DateTime orderDate,
        OrderStatus status,
        params (long ProductId, int Quantity, decimal UnitPrice)[] lines)
    {
        var order = new Order
        {
            CustomerId = customerId,
            OrderDate = orderDate,
            Status = status,
            TotalAmount = lines.Sum(line => line.Quantity * line.UnitPrice),
            CreatedAt = orderDate
        };
        foreach (var line in lines)
        {
            order.Items.Add(new OrderItem
            {
                ProductId = line.ProductId,
                Quantity = line.Quantity,
                UnitPrice = line.UnitPrice,
                LineTotal = line.Quantity * line.UnitPrice
            });
        }

        return order;
    }

    private static Task<TopProductsResponse> GetTopProductsAsync(HttpClient client) =>
        GetRequiredAsync<TopProductsResponse>(
            client,
            $"/api/analytics/top-products?from={From}&to={To}&metric=Quantity&limit=5");

    private static Task<SalesSummaryResponse> GetSummaryAsync(HttpClient client) =>
        GetRequiredAsync<SalesSummaryResponse>(
            client,
            $"/api/analytics/summary?from={From}&to={To}");

    private static Task<CategoryPerformanceResponse> GetCategoriesAsync(HttpClient client) =>
        GetRequiredAsync<CategoryPerformanceResponse>(
            client,
            $"/api/analytics/categories?from={From}&to={To}");

    private static async Task<T> GetRequiredAsync<T>(HttpClient client, string uri)
    {
        using var response = await client.GetAsync(uri);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<T>()
            ?? throw new InvalidOperationException($"{typeof(T).Name} response was empty.");
    }

    private static async Task<AiSnapshot> AskAiAsync(HttpClient client)
    {
        using var response = await client.PostAsJsonAsync(
            "/api/ai/query",
            new { Question });
        response.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync());
        var root = document.RootElement;
        Assert.Equal("Completed", root.GetProperty("status").GetString());
        return new AiSnapshot(
            root.GetProperty("data").Clone(),
            root.GetProperty("explanation").GetProperty("text").GetString()
                ?? string.Empty);
    }

    private sealed record DemoFixture(
        long CustomerId,
        string BaselineLeaderName,
        long TargetProductId,
        string TargetProductName,
        long TargetCategoryId);

    private sealed record GrowthDelta(long Quantity, decimal Revenue);

    private sealed record AiSnapshot(JsonElement Data, string ExplanationText);
}
