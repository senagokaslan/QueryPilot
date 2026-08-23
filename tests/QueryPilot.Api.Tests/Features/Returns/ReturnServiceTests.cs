using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using QueryPilot.Api.Common.Exceptions;
using QueryPilot.Api.Data;
using QueryPilot.Api.Features.Categories;
using QueryPilot.Api.Features.Customers;
using QueryPilot.Api.Features.Orders;
using QueryPilot.Api.Features.Products;
using QueryPilot.Api.Features.Returns;
using QueryPilot.Api.Features.Returns.Dtos;
using QueryPilot.Api.Tests.TestSupport;
using Xunit;

namespace QueryPilot.Api.Tests.Features.Returns;

public sealed class ReturnServiceTests
{
    [Fact]
    public async Task Partial_then_full_return_tracks_remaining_quantity_and_snapshot_amounts()
    {
        await using var database = await ReturnTestDatabase.CreateAsync();
        var dbContext = database.Context;
        var saveChanges = database.SaveChanges;
        var fixture = await SeedCompletedOrderItemAsync(dbContext, quantity: 5, unitPrice: 100m);
        saveChanges.Reset();
        var service = CreateService(dbContext);

        var partial = await service.CreateAsync(CreateRequest(fixture.Item.Id, 2));
        var full = await service.CreateAsync(CreateRequest(fixture.Item.Id, 3));

        Assert.Equal(200m, partial.Amount);
        Assert.Equal(3, partial.RemainingReturnableQuantity);
        Assert.Equal(300m, full.Amount);
        Assert.Equal(0, full.RemainingReturnableQuantity);
        Assert.Equal(5, await dbContext.Returns.SumAsync(item => item.Quantity));
        Assert.Equal(
            500m,
            (await dbContext.Returns.Select(item => item.Amount).ToListAsync()).Sum());
        Assert.Equal(2, saveChanges.CallCount);
    }

    [Fact]
    public async Task Return_exceeding_remaining_quantity_including_previous_returns_is_rejected_without_saving()
    {
        await using var database = await ReturnTestDatabase.CreateAsync();
        var dbContext = database.Context;
        var saveChanges = database.SaveChanges;
        var fixture = await SeedCompletedOrderItemAsync(dbContext, quantity: 5, unitPrice: 100m);
        var service = CreateService(dbContext);
        await service.CreateAsync(CreateRequest(fixture.Item.Id, 2));
        saveChanges.Reset();

        await Assert.ThrowsAsync<ConflictException>(() =>
            service.CreateAsync(CreateRequest(fixture.Item.Id, 4)));

        Assert.Equal(0, saveChanges.CallCount);
        Assert.Equal(2, await dbContext.Returns.SumAsync(item => item.Quantity));
    }

    [Fact]
    public async Task Return_amount_uses_order_item_price_snapshot_after_product_price_changes()
    {
        await using var database = await ReturnTestDatabase.CreateAsync();
        var dbContext = database.Context;
        var saveChanges = database.SaveChanges;
        var fixture = await SeedCompletedOrderItemAsync(dbContext, quantity: 5, unitPrice: 100m);
        fixture.Product.UnitPrice = 999m;
        await dbContext.SaveChangesAsync();
        saveChanges.Reset();
        var service = CreateService(dbContext);

        var result = await service.CreateAsync(CreateRequest(fixture.Item.Id, 2));

        Assert.Equal(200m, result.Amount);
        var savedReturn = await dbContext.Returns.SingleAsync();
        Assert.Equal(200m, savedReturn.Amount);
        Assert.Equal(100m, fixture.Item.UnitPrice);
        Assert.Equal(1, saveChanges.CallCount);
    }

    [Fact]
    public async Task Returning_missing_order_item_throws_not_found_without_saving()
    {
        await using var database = await ReturnTestDatabase.CreateAsync();
        var dbContext = database.Context;
        var saveChanges = database.SaveChanges;
        var service = CreateService(dbContext);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            service.CreateAsync(CreateRequest(orderItemId: 404, quantity: 1)));

        Assert.Equal(0, saveChanges.CallCount);
        Assert.Empty(dbContext.Returns);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task Returning_zero_or_negative_quantity_fails_validation_without_saving(
        int quantity)
    {
        await using var database = await ReturnTestDatabase.CreateAsync();
        var dbContext = database.Context;
        var saveChanges = database.SaveChanges;
        var service = CreateService(dbContext);

        var exception = await Assert.ThrowsAsync<RequestValidationException>(() =>
            service.CreateAsync(CreateRequest(orderItemId: 1, quantity)));

        Assert.Contains(nameof(CreateReturnRequest.Quantity), exception.Errors.Keys);
        Assert.Equal(0, saveChanges.CallCount);
        Assert.Empty(dbContext.Returns);
    }

    private static ReturnService CreateService(AppDbContext dbContext) =>
        new(dbContext, NullLogger<ReturnService>.Instance);

    private static CreateReturnRequest CreateRequest(long orderItemId, int quantity) =>
        new()
        {
            OrderItemId = orderItemId,
            Quantity = quantity,
            Reason = "  Test reason  "
        };

    private static async Task<ReturnFixture> SeedCompletedOrderItemAsync(
        AppDbContext dbContext,
        int quantity,
        decimal unitPrice)
    {
        var createdAt = DateTime.UtcNow;
        var category = new Category
        {
            Id = 1,
            Name = "Test Category",
            IsActive = true,
            CreatedAt = createdAt
        };
        var product = new Product
        {
            Id = 1,
            Name = "Test Product",
            SKU = "TEST-1",
            CategoryId = category.Id,
            Category = category,
            UnitPrice = unitPrice,
            IsActive = true,
            CreatedAt = createdAt
        };
        var customer = new Customer
        {
            Id = 1,
            Name = "Test Customer",
            Email = "test@example.com",
            NormalizedEmail = "test@example.com",
            City = "Ankara",
            IsActive = true,
            CreatedAt = createdAt
        };
        var order = new Order
        {
            Id = 1,
            CustomerId = customer.Id,
            Customer = customer,
            OrderDate = createdAt,
            Status = OrderStatus.Completed,
            TotalAmount = quantity * unitPrice,
            CreatedAt = createdAt
        };
        var item = new OrderItem
        {
            Id = 1,
            OrderId = order.Id,
            Order = order,
            ProductId = product.Id,
            Product = product,
            Quantity = quantity,
            UnitPrice = unitPrice,
            LineTotal = quantity * unitPrice
        };
        dbContext.AddRange(category, product, customer, order, item);
        await dbContext.SaveChangesAsync();
        return new ReturnFixture(product, item);
    }

    private sealed record ReturnFixture(Product Product, OrderItem Item);
}
