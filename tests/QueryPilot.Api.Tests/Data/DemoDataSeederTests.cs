using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using QueryPilot.Api.Data;
using QueryPilot.Api.Data.Seed;
using QueryPilot.Api.Features.Orders;
using Xunit;

namespace QueryPilot.Api.Tests.Data;

public sealed class DemoDataSeederTests
{
    [Fact]
    public async Task Demo_seed_populates_every_analytics_surface_and_is_idempotent()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;
        await using var dbContext = new AppDbContext(options);
        await dbContext.Database.EnsureCreatedAsync();
        var seeder = new DemoDataSeeder(
            dbContext,
            NullLogger<DemoDataSeeder>.Instance);

        await seeder.SeedAsync();

        Assert.Equal(10, await dbContext.Categories.CountAsync());
        Assert.Equal(100, await dbContext.Products.CountAsync());
        Assert.Equal(500, await dbContext.Customers.CountAsync());
        Assert.Equal(1_200, await dbContext.Orders.CountAsync());
        Assert.True(await dbContext.Returns.AnyAsync());
        Assert.True(await dbContext.Orders.AnyAsync(
            order => order.Status == OrderStatus.Completed));
        Assert.True(await dbContext.Orders.AnyAsync(
            order => order.Status == OrderStatus.Pending));
        Assert.True(await dbContext.Orders.AnyAsync(
            order => order.Status == OrderStatus.Cancelled));
        Assert.Equal(4, await dbContext.Returns
            .Select(item => item.Reason)
            .Distinct()
            .CountAsync());

        var toUtc = DateTime.UtcNow.Date.AddDays(1);
        var fromUtc = toUtc.AddYears(-1);
        var recentFromUtc = toUtc.AddDays(-90);
        var previousFromUtc = recentFromUtc.AddDays(-90);
        var orders = await dbContext.Orders
            .Include(order => order.Items)
                .ThenInclude(item => item.Product)
                    .ThenInclude(product => product.Category)
            .AsNoTracking()
            .ToListAsync();
        var completedOrders = orders
            .Where(order => order.Status == OrderStatus.Completed)
            .ToArray();
        var currentOrders = completedOrders
            .Where(order => order.OrderDate >= fromUtc && order.OrderDate < toUtc)
            .ToArray();
        var recentOrders = completedOrders
            .Where(order => order.OrderDate >= recentFromUtc
                && order.OrderDate < toUtc)
            .ToArray();
        var previousOrders = completedOrders
            .Where(order => order.OrderDate >= previousFromUtc
                && order.OrderDate < recentFromUtc)
            .ToArray();

        Assert.True(currentOrders.Sum(order => order.TotalAmount) > 0);
        Assert.True(currentOrders.Sum(order => order.Items.Sum(item => item.Quantity)) > 0);
        Assert.NotEmpty(previousOrders);
        Assert.True(currentOrders
            .Select(order => new { order.OrderDate.Year, order.OrderDate.Month })
            .Distinct()
            .Count() >= 12);

        var recentItems = recentOrders.SelectMany(order => order.Items).ToArray();
        Assert.True(recentItems
            .GroupBy(item => item.ProductId)
            .Count(group => group.Sum(item => item.Quantity) > 0) >= 5);
        Assert.True(recentItems
            .GroupBy(item => item.ProductId)
            .Count(group => group.Sum(item => item.LineTotal) > 0) >= 5);
        var categoryRevenue = recentItems
            .GroupBy(item => item.Product.CategoryId)
            .ToDictionary(
                group => group.Key,
                group => group.Sum(item => item.LineTotal));
        Assert.Equal(10, categoryRevenue.Count);
        Assert.All(categoryRevenue.Values, revenue => Assert.True(revenue > 0));

        var returns = await dbContext.Returns.AsNoTracking().ToListAsync();
        Assert.True(returns.Sum(item => item.Quantity) > 0);
        Assert.True(returns.Sum(item => item.Amount) > 0);
        Assert.Equal(4, returns.Select(item => item.Reason).Distinct().Count());
        Assert.True(returns.Select(item => item.OrderItemId).Distinct().Count() >= 5);

        var countsBeforeSecondRun = new
        {
            Categories = await dbContext.Categories.CountAsync(),
            Products = await dbContext.Products.CountAsync(),
            Customers = await dbContext.Customers.CountAsync(),
            Orders = await dbContext.Orders.CountAsync(),
            Returns = await dbContext.Returns.CountAsync()
        };
        await seeder.SeedAsync();
        var countsAfterSecondRun = new
        {
            Categories = await dbContext.Categories.CountAsync(),
            Products = await dbContext.Products.CountAsync(),
            Customers = await dbContext.Customers.CountAsync(),
            Orders = await dbContext.Orders.CountAsync(),
            Returns = await dbContext.Returns.CountAsync()
        };

        Assert.Equal(countsBeforeSecondRun, countsAfterSecondRun);
    }
}
