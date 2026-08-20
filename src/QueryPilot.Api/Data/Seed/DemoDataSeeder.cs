using Microsoft.EntityFrameworkCore;
using QueryPilot.Api.Features.Categories;
using QueryPilot.Api.Features.Customers;
using QueryPilot.Api.Features.Orders;
using QueryPilot.Api.Features.Products;
using QueryPilot.Api.Features.Returns;

namespace QueryPilot.Api.Data.Seed;

public sealed class DemoDataSeeder(
    AppDbContext dbContext,
    ILogger<DemoDataSeeder> logger)
{
    private const int CategoryCount = 10;
    private const int ProductCount = 100;
    private const int CustomerCount = 500;
    private const int OrderCount = 1_200;
    private const int RandomSeed = 20260820;

    private static readonly string[] CategoryNames =
    [
        "Electronics", "Home and Living", "Books", "Sports", "Clothing",
        "Personal Care", "Toys", "Office", "Garden", "Pet Supplies"
    ];

    private static readonly string[] Cities =
    [
        "Istanbul", "Ankara", "Izmir", "Bursa", "Antalya",
        "Adana", "Konya", "Gaziantep", "Mersin", "Eskisehir"
    ];

    private static readonly string[] ReturnReasons =
    [
        "Damaged item", "Wrong item", "Not as expected", "Changed mind"
    ];

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        if (await ContainsBusinessDataAsync(cancellationToken))
        {
            logger.LogInformation("Demo seed skipped because business data already exists.");
            return;
        }

        await using var transaction =
            await dbContext.Database.BeginTransactionAsync(cancellationToken);

        var random = new Random(RandomSeed);
        var nowUtc = DateTime.UtcNow;
        var periodStartUtc = nowUtc.Date.AddYears(-1);
        var lastOrderDateUtc = nowUtc.Date.AddDays(-1);

        var categories = CreateCategories(periodStartUtc);
        dbContext.Categories.AddRange(categories);
        await dbContext.SaveChangesAsync(cancellationToken);

        var products = CreateProducts(categories, periodStartUtc);
        var customers = CreateCustomers(periodStartUtc);
        dbContext.Products.AddRange(products);
        dbContext.Customers.AddRange(customers);
        await dbContext.SaveChangesAsync(cancellationToken);

        var orders = CreateOrders(
            customers,
            products,
            random,
            periodStartUtc,
            lastOrderDateUtc);
        dbContext.Orders.AddRange(orders);
        await dbContext.SaveChangesAsync(cancellationToken);

        var returns = CreateReturns(orders, random, nowUtc);
        dbContext.Returns.AddRange(returns);
        await dbContext.SaveChangesAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        logger.LogInformation(
            "Demo seed completed: {CategoryCount} categories, {ProductCount} products, " +
            "{CustomerCount} customers, {OrderCount} orders and {ReturnCount} returns.",
            categories.Count,
            products.Count,
            customers.Count,
            orders.Count,
            returns.Count);
    }

    private async Task<bool> ContainsBusinessDataAsync(CancellationToken cancellationToken)
    {
        return await dbContext.Categories.AnyAsync(cancellationToken)
            || await dbContext.Products.AnyAsync(cancellationToken)
            || await dbContext.Customers.AnyAsync(cancellationToken)
            || await dbContext.Orders.AnyAsync(cancellationToken)
            || await dbContext.OrderItems.AnyAsync(cancellationToken)
            || await dbContext.Returns.AnyAsync(cancellationToken);
    }

    private static List<Category> CreateCategories(DateTime createdAt)
    {
        return CategoryNames
            .Take(CategoryCount)
            .Select(name => new Category
            {
                Name = name,
                IsActive = true,
                CreatedAt = createdAt
            })
            .ToList();
    }

    private static List<Product> CreateProducts(
        IReadOnlyList<Category> categories,
        DateTime periodStartUtc)
    {
        return Enumerable.Range(1, ProductCount)
            .Select(number => new Product
            {
                Name = $"Demo Product {number:000}",
                SKU = $"QP-{number:0000}",
                CategoryId = categories[(number - 1) % categories.Count].Id,
                UnitPrice = decimal.Round(9.90m + number * 3.17m, 2),
                IsActive = number % 20 != 0,
                CreatedAt = periodStartUtc.AddDays(number % 45)
            })
            .ToList();
    }

    private static List<Customer> CreateCustomers(DateTime periodStartUtc)
    {
        return Enumerable.Range(1, CustomerCount)
            .Select(number =>
            {
                var email = $"demo.customer{number:000}@example.test";

                return new Customer
                {
                    Name = $"Demo Customer {number:000}",
                    Email = email,
                    NormalizedEmail = email.ToLowerInvariant(),
                    City = Cities[(number * 7 + number / 25) % Cities.Length],
                    IsActive = number % 25 != 0,
                    CreatedAt = periodStartUtc.AddDays(number % 120)
                };
            })
            .ToList();
    }

    private static List<Order> CreateOrders(
        IReadOnlyList<Customer> customers,
        IReadOnlyList<Product> products,
        Random random,
        DateTime periodStartUtc,
        DateTime lastOrderDateUtc)
    {
        var orders = new List<Order>(OrderCount);
        var periodLength = (lastOrderDateUtc - periodStartUtc).Days;

        for (var orderNumber = 0; orderNumber < OrderCount; orderNumber++)
        {
            var orderDate = periodStartUtc
                .AddDays(random.Next(periodLength + 1))
                .AddHours(random.Next(8, 22))
                .AddMinutes(random.Next(60));
            var statusRoll = random.Next(100);
            var order = new Order
            {
                CustomerId = customers[random.Next(customers.Count)].Id,
                OrderDate = orderDate,
                Status = statusRoll < 82
                    ? OrderStatus.Completed
                    : statusRoll < 93
                        ? OrderStatus.Cancelled
                        : OrderStatus.Pending,
                CreatedAt = orderDate
            };

            var selectedProductIndexes = new HashSet<int>();
            var itemCount = random.Next(1, 6);

            while (selectedProductIndexes.Count < itemCount)
            {
                selectedProductIndexes.Add(random.Next(products.Count));
            }

            foreach (var productIndex in selectedProductIndexes)
            {
                var product = products[productIndex];
                var quantity = random.Next(1, 5);
                var lineTotal = product.UnitPrice * quantity;

                order.Items.Add(new OrderItem
                {
                    ProductId = product.Id,
                    Quantity = quantity,
                    UnitPrice = product.UnitPrice,
                    LineTotal = lineTotal
                });
            }

            order.TotalAmount = order.Items.Sum(item => item.LineTotal);
            orders.Add(order);
        }

        return orders;
    }

    private static List<Return> CreateReturns(
        IEnumerable<Order> orders,
        Random random,
        DateTime nowUtc)
    {
        var returns = new List<Return>();

        foreach (var order in orders.Where(order => order.Status == OrderStatus.Completed))
        {
            foreach (var item in order.Items.Where(_ => random.Next(100) < 9))
            {
                var quantity = random.Next(1, item.Quantity + 1);
                var availableDays = Math.Max(
                    0,
                    Math.Min(30, (nowUtc - order.OrderDate).Days));
                var returnDate = order.OrderDate.AddDays(random.Next(availableDays + 1));

                returns.Add(new Return
                {
                    OrderItemId = item.Id,
                    Quantity = quantity,
                    Reason = ReturnReasons[random.Next(ReturnReasons.Length)],
                    ReturnDate = returnDate,
                    Amount = item.UnitPrice * quantity
                });
            }
        }

        return returns;
    }
}
