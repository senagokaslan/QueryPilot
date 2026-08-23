using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Npgsql;
using QueryPilot.Api.Data;
using QueryPilot.Api.Features.Categories;
using QueryPilot.Api.Features.Customers;
using QueryPilot.Api.Features.Orders;
using QueryPilot.Api.Features.Products;
using ReturnEntity = QueryPilot.Api.Features.Returns.Return;
using Xunit;

namespace QueryPilot.Api.IntegrationTests;

public sealed class PostgreSqlDatabaseFixture : IAsyncLifetime
{
    public const string ConnectionEnvironmentVariable =
        "QUERYPILOT_INTEGRATION_ADMIN_CONNECTION_STRING";
    public const string ConnectionUserSecret =
        "IntegrationTests:AdminConnectionString";
    public const string TestDatabasePrefix = "querypilot_test_";

    private string? adminConnectionString;

    public bool IsEnabled { get; private set; }

    public string DatabaseName { get; private set; } = string.Empty;

    public string TestConnectionString { get; private set; } = string.Empty;

    public async Task InitializeAsync()
    {
        var configuredConnection = ResolveConfiguredConnectionString();
        if (string.IsNullOrWhiteSpace(configuredConnection))
        {
            return;
        }

        var configured = new NpgsqlConnectionStringBuilder(configuredConnection);
        EnsureLoopbackHost(configured.Host ?? string.Empty);

        DatabaseName = $"{TestDatabasePrefix}{Guid.NewGuid():N}";
        var admin = new NpgsqlConnectionStringBuilder(configured.ConnectionString)
        {
            Database = "postgres",
            Pooling = false
        };
        adminConnectionString = admin.ConnectionString;
        var test = new NpgsqlConnectionStringBuilder(configured.ConnectionString)
        {
            Database = DatabaseName,
            Pooling = false
        };
        TestConnectionString = test.ConnectionString;

        try
        {
            await ExecuteAdminCommandAsync(
                $"""CREATE DATABASE "{DatabaseName}" TEMPLATE template0 ENCODING 'UTF8'""");
            await using var dbContext = CreateContext();
            await dbContext.Database.MigrateAsync();
            await SeedFixtureAsync(dbContext);
            IsEnabled = true;
        }
        catch
        {
            await DropDatabaseIfCreatedAsync();
            throw;
        }
    }

    public static string? ResolveConfiguredConnectionString()
    {
        var environmentConnection = Environment.GetEnvironmentVariable(
            ConnectionEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(environmentConnection))
        {
            return environmentConnection;
        }

        return new ConfigurationBuilder()
            .AddUserSecrets<PostgreSqlDatabaseFixture>(optional: true)
            .Build()[ConnectionUserSecret];
    }

    public AppDbContext CreateContext()
    {
        if (string.IsNullOrEmpty(TestConnectionString))
        {
            throw new InvalidOperationException(
                $"Set {ConnectionEnvironmentVariable} to run PostgreSQL integration tests.");
        }

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(TestConnectionString)
            .Options;
        return new AppDbContext(options);
    }

    public async Task DisposeAsync()
    {
        if (string.IsNullOrEmpty(DatabaseName))
        {
            return;
        }

        await DropDatabaseIfCreatedAsync();

        await using var connection = new NpgsqlConnection(adminConnectionString);
        await connection.OpenAsync();
        await using var verify = new NpgsqlCommand(
            "SELECT COUNT(*) FROM pg_database WHERE datname = @databaseName",
            connection);
        verify.Parameters.AddWithValue("databaseName", DatabaseName);
        var count = Convert.ToInt32(await verify.ExecuteScalarAsync());
        if (count != 0)
        {
            throw new InvalidOperationException(
                "The disposable PostgreSQL test database was not removed.");
        }
    }

    private async Task DropDatabaseIfCreatedAsync()
    {
        if (string.IsNullOrEmpty(adminConnectionString)
            || !DatabaseName.StartsWith(TestDatabasePrefix, StringComparison.Ordinal)
            || DatabaseName.Length != TestDatabasePrefix.Length + 32)
        {
            return;
        }

        NpgsqlConnection.ClearAllPools();
        await ExecuteAdminCommandAsync(
            $"""DROP DATABASE IF EXISTS "{DatabaseName}" WITH (FORCE)""");
    }

    private async Task ExecuteAdminCommandAsync(string commandText)
    {
        await using var connection = new NpgsqlConnection(adminConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(commandText, connection);
        await command.ExecuteNonQueryAsync();
    }

    private static void EnsureLoopbackHost(string host)
    {
        var isLoopback = string.Equals(
                host,
                "localhost",
                StringComparison.OrdinalIgnoreCase)
            || IPAddress.TryParse(host, out var address) && IPAddress.IsLoopback(address);

        if (!isLoopback)
        {
            throw new InvalidOperationException(
                "Integration tests accept only a loopback PostgreSQL host.");
        }
    }

    private static async Task SeedFixtureAsync(AppDbContext dbContext)
    {
        var createdAt = new DateTime(2024, 12, 1, 0, 0, 0, DateTimeKind.Utc);
        var electronics = new Category
        {
            Id = 1,
            Name = "Electronics",
            IsActive = true,
            CreatedAt = createdAt
        };
        var home = new Category
        {
            Id = 2,
            Name = "Home",
            IsActive = true,
            CreatedAt = createdAt
        };
        var alpha = CreateProduct(1, "Alpha", "ALPHA", electronics, 10m, createdAt);
        var beta = CreateProduct(2, "Beta", "BETA", electronics, 100m, createdAt);
        var cup = CreateProduct(3, "Cup", "CUP", home, 20m, createdAt);
        var customer = new Customer
        {
            Id = 1,
            Name = "Integration Customer",
            Email = "integration@example.com",
            NormalizedEmail = "integration@example.com",
            City = "Ankara",
            IsActive = true,
            CreatedAt = createdAt
        };
        var previousOrder = CreateOrder(
            1,
            customer,
            Utc(2024, 12, 25),
            totalAmount: 150m);
        var currentOrderOne = CreateOrder(
            2,
            customer,
            Utc(2024, 12, 30),
            totalAmount: 300m);
        var currentOrderTwo = CreateOrder(
            3,
            customer,
            Utc(2025, 1, 2),
            totalAmount: 100m);
        var cancelledOrder = CreateOrder(
            4,
            customer,
            Utc(2025, 1, 3),
            totalAmount: 999m,
            OrderStatus.Cancelled);
        var previousAlpha = CreateItem(1, previousOrder, alpha, 5, 10m);
        var previousCup = CreateItem(2, previousOrder, cup, 5, 20m);
        var currentAlpha = CreateItem(3, currentOrderOne, alpha, 10, 10m);
        var currentBeta = CreateItem(4, currentOrderOne, beta, 2, 100m);
        var currentCup = CreateItem(5, currentOrderTwo, cup, 5, 20m);
        var cancelledAlpha = CreateItem(6, cancelledOrder, alpha, 99, 10.09m);
        var defectiveOne = CreateReturn(
            1, currentAlpha, 3, "Defective", Utc(2025, 1, 3), 30m);
        var defectiveTwo = CreateReturn(
            2, currentAlpha, 2, "Defective", Utc(2025, 1, 4), 20m);
        var changedMind = CreateReturn(
            3, currentCup, 1, "Changed mind", Utc(2025, 1, 4), 20m);
        var noSalesBaseline = CreateReturn(
            4, currentAlpha, 1, "Late inspection", Utc(2030, 1, 1), 10m);

        dbContext.AddRange(
            electronics,
            home,
            alpha,
            beta,
            cup,
            customer,
            previousOrder,
            currentOrderOne,
            currentOrderTwo,
            cancelledOrder,
            previousAlpha,
            previousCup,
            currentAlpha,
            currentBeta,
            currentCup,
            cancelledAlpha,
            defectiveOne,
            defectiveTwo,
            changedMind,
            noSalesBaseline);
        await dbContext.SaveChangesAsync();
    }

    private static Product CreateProduct(
        long id,
        string name,
        string sku,
        Category category,
        decimal price,
        DateTime createdAt) =>
        new()
        {
            Id = id,
            Name = name,
            SKU = sku,
            CategoryId = category.Id,
            Category = category,
            UnitPrice = price,
            IsActive = true,
            CreatedAt = createdAt
        };

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

    private static OrderItem CreateItem(
        long id,
        Order order,
        Product product,
        int quantity,
        decimal price) =>
        new()
        {
            Id = id,
            OrderId = order.Id,
            Order = order,
            ProductId = product.Id,
            Product = product,
            Quantity = quantity,
            UnitPrice = price,
            LineTotal = quantity * price
        };

    private static ReturnEntity CreateReturn(
        long id,
        OrderItem item,
        int quantity,
        string reason,
        DateTime returnDate,
        decimal amount) =>
        new()
        {
            Id = id,
            OrderItemId = item.Id,
            OrderItem = item,
            Quantity = quantity,
            Reason = reason,
            ReturnDate = returnDate,
            Amount = amount
        };

    private static DateTime Utc(int year, int month, int day) =>
        new(year, month, day, 12, 0, 0, DateTimeKind.Utc);
}
