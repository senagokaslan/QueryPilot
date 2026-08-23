using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using QueryPilot.Api.Common.Exceptions;
using QueryPilot.Api.Common.Pagination;
using QueryPilot.Api.Data;
using QueryPilot.Api.Features.Categories;
using QueryPilot.Api.Features.Customers;
using QueryPilot.Api.Features.Orders;
using QueryPilot.Api.Features.Orders.Dtos;
using QueryPilot.Api.Features.Products;
using QueryPilot.Api.Tests.TestSupport;
using Xunit;

namespace QueryPilot.Api.Tests.Features.Orders;

public sealed class OrderServiceTests
{
    [Fact]
    public async Task Creating_order_calculates_line_totals_and_order_total_from_product_prices()
    {
        var saveChanges = new CountingSaveChangesInterceptor();
        await using var dbContext = TestDbContextFactory.Create(saveChanges);
        var customer = CreateCustomer(id: 1, isActive: true);
        var category = CreateCategory();
        var firstProduct = CreateProduct(1, category, 12.50m, isActive: true);
        var secondProduct = CreateProduct(2, category, 20m, isActive: true);
        dbContext.AddRange(customer, category, firstProduct, secondProduct);
        await dbContext.SaveChangesAsync();
        saveChanges.Reset();
        var service = CreateService(dbContext);

        var result = await service.CreateAsync(new CreateOrderRequest
        {
            CustomerId = customer.Id,
            Items =
            [
                new CreateOrderItemRequest { ProductId = firstProduct.Id, Quantity = 2 },
                new CreateOrderItemRequest { ProductId = secondProduct.Id, Quantity = 3 }
            ]
        });

        Assert.Equal(85m, result.TotalAmount);
        Assert.Collection(
            result.Items.OrderBy(item => item.Product.Id),
            item =>
            {
                Assert.Equal(12.50m, item.UnitPrice);
                Assert.Equal(25m, item.LineTotal);
            },
            item =>
            {
                Assert.Equal(20m, item.UnitPrice);
                Assert.Equal(60m, item.LineTotal);
            });
        var savedOrder = await dbContext.Orders
            .Include(order => order.Items)
            .SingleAsync(order => order.Id == result.Id);
        Assert.Equal(savedOrder.Items.Sum(item => item.LineTotal), savedOrder.TotalAmount);
        Assert.Equal(1, saveChanges.CallCount);
    }

    [Fact]
    public void Client_supplied_unit_price_line_total_and_order_total_are_rejected()
    {
        const string json = """
            {
              "customerId": 1,
              "totalAmount": 1,
              "items": [
                {
                  "productId": 1,
                  "quantity": 2,
                  "unitPrice": 0.01,
                  "lineTotal": 0.02
                }
              ]
            }
            """;

        Assert.Throws<JsonException>(() =>
            JsonSerializer.Deserialize<CreateOrderRequest>(
                json,
                new JsonSerializerOptions(JsonSerializerDefaults.Web)));
    }

    [Fact]
    public async Task Changing_product_price_after_order_does_not_change_order_item_snapshot()
    {
        var saveChanges = new CountingSaveChangesInterceptor();
        await using var dbContext = TestDbContextFactory.Create(saveChanges);
        var customer = CreateCustomer(id: 1, isActive: true);
        var category = CreateCategory();
        var product = CreateProduct(1, category, 100m, isActive: true);
        dbContext.AddRange(customer, category, product);
        await dbContext.SaveChangesAsync();
        var service = CreateService(dbContext);
        var order = await service.CreateAsync(CreateRequest(customer.Id, product.Id, 2));

        product.UnitPrice = 999m;
        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();

        var savedItem = await dbContext.OrderItems
            .SingleAsync(item => item.OrderId == order.Id);
        var savedOrder = await dbContext.Orders.SingleAsync(item => item.Id == order.Id);
        Assert.Equal(100m, savedItem.UnitPrice);
        Assert.Equal(200m, savedItem.LineTotal);
        Assert.Equal(200m, savedOrder.TotalAmount);
    }

    [Fact]
    public async Task Creating_order_for_missing_customer_throws_not_found_without_saving()
    {
        var saveChanges = new CountingSaveChangesInterceptor();
        await using var dbContext = TestDbContextFactory.Create(saveChanges);
        var service = CreateService(dbContext);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            service.CreateAsync(CreateRequest(customerId: 404, productId: 1, quantity: 1)));

        Assert.Equal(0, saveChanges.CallCount);
        Assert.Empty(dbContext.Orders);
    }

    [Fact]
    public async Task Creating_order_for_inactive_customer_throws_conflict_without_saving()
    {
        var saveChanges = new CountingSaveChangesInterceptor();
        await using var dbContext = TestDbContextFactory.Create(saveChanges);
        dbContext.Customers.Add(CreateCustomer(id: 1, isActive: false));
        await dbContext.SaveChangesAsync();
        saveChanges.Reset();
        var service = CreateService(dbContext);

        await Assert.ThrowsAsync<ConflictException>(() =>
            service.CreateAsync(CreateRequest(customerId: 1, productId: 1, quantity: 1)));

        Assert.Equal(0, saveChanges.CallCount);
        Assert.Empty(dbContext.Orders);
    }

    [Fact]
    public async Task Creating_order_with_missing_product_throws_not_found_without_saving()
    {
        var saveChanges = new CountingSaveChangesInterceptor();
        await using var dbContext = TestDbContextFactory.Create(saveChanges);
        dbContext.Customers.Add(CreateCustomer(id: 1, isActive: true));
        await dbContext.SaveChangesAsync();
        saveChanges.Reset();
        var service = CreateService(dbContext);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            service.CreateAsync(CreateRequest(customerId: 1, productId: 404, quantity: 1)));

        Assert.Equal(0, saveChanges.CallCount);
        Assert.Empty(dbContext.Orders);
    }

    [Fact]
    public async Task Creating_order_with_inactive_product_throws_conflict_without_saving()
    {
        var saveChanges = new CountingSaveChangesInterceptor();
        await using var dbContext = TestDbContextFactory.Create(saveChanges);
        var category = CreateCategory();
        dbContext.AddRange(
            CreateCustomer(id: 1, isActive: true),
            category,
            CreateProduct(1, category, 100m, isActive: false));
        await dbContext.SaveChangesAsync();
        saveChanges.Reset();
        var service = CreateService(dbContext);

        await Assert.ThrowsAsync<ConflictException>(() =>
            service.CreateAsync(CreateRequest(customerId: 1, productId: 1, quantity: 1)));

        Assert.Equal(0, saveChanges.CallCount);
        Assert.Empty(dbContext.Orders);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task Creating_order_with_zero_or_negative_quantity_fails_validation_without_saving(
        int quantity)
    {
        var saveChanges = new CountingSaveChangesInterceptor();
        await using var dbContext = TestDbContextFactory.Create(saveChanges);
        var service = CreateService(dbContext);

        var exception = await Assert.ThrowsAsync<RequestValidationException>(() =>
            service.CreateAsync(CreateRequest(customerId: 1, productId: 1, quantity)));

        Assert.Contains("Items[0].Quantity", exception.Errors.Keys);
        Assert.Equal(0, saveChanges.CallCount);
        Assert.Empty(dbContext.Orders);
    }

    private static OrderService CreateService(AppDbContext dbContext) =>
        new(
            dbContext,
            NullLogger<OrderService>.Instance,
            Options.Create(new PaginationOptions()));

    private static CreateOrderRequest CreateRequest(
        long customerId,
        long productId,
        int quantity) =>
        new()
        {
            CustomerId = customerId,
            Items =
            [
                new CreateOrderItemRequest
                {
                    ProductId = productId,
                    Quantity = quantity
                }
            ]
        };

    private static Category CreateCategory() =>
        new()
        {
            Id = 1,
            Name = "Test Category",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

    private static Customer CreateCustomer(long id, bool isActive) =>
        new()
        {
            Id = id,
            Name = "Test Customer",
            Email = $"customer-{id}@example.com",
            NormalizedEmail = $"customer-{id}@example.com",
            City = "Ankara",
            IsActive = isActive,
            CreatedAt = DateTime.UtcNow
        };

    private static Product CreateProduct(
        long id,
        Category category,
        decimal unitPrice,
        bool isActive) =>
        new()
        {
            Id = id,
            Name = $"Product {id}",
            SKU = $"SKU-{id}",
            CategoryId = category.Id,
            Category = category,
            UnitPrice = unitPrice,
            IsActive = isActive,
            CreatedAt = DateTime.UtcNow
        };
}
