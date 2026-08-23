using Microsoft.EntityFrameworkCore;
using QueryPilot.Api.Features.Analytics;
using Xunit;

namespace QueryPilot.Api.IntegrationTests;

[Collection(PostgreSqlIntegrationCollection.Name)]
public sealed class AnalyticsPostgreSqlIntegrationTests(
    PostgreSqlDatabaseFixture database)
{
    private static readonly DateTimeOffset CurrentFrom =
        new(2024, 12, 30, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset CurrentTo =
        new(2025, 1, 6, 0, 0, 0, TimeSpan.Zero);

    [PostgreSqlFact]
    public async Task Disposable_database_uses_test_prefix_and_has_all_migrations()
    {
        if (!database.IsEnabled)
        {
            return;
        }

        Assert.StartsWith(
            PostgreSqlDatabaseFixture.TestDatabasePrefix,
            database.DatabaseName,
            StringComparison.Ordinal);
        Assert.DoesNotContain("dev", database.DatabaseName, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("prod", database.DatabaseName, StringComparison.OrdinalIgnoreCase);
        await using var dbContext = database.CreateContext();
        var appliedMigrations = await dbContext.Database
            .GetAppliedMigrationsAsync();

        Assert.Equal(2, appliedMigrations.Count());
        Assert.Empty(await dbContext.Database.GetPendingMigrationsAsync());
    }

    [PostgreSqlFact]
    public async Task Sales_summary_matches_hand_calculated_revenue_orders_units_and_average()
    {
        if (!database.IsEnabled)
        {
            return;
        }

        await using var dbContext = database.CreateContext();
        var service = new AnalyticsService(dbContext);

        var result = await service.GetSalesSummaryAsync(CurrentFrom, CurrentTo);

        Assert.Equal(400m, result.TotalRevenue);
        Assert.Equal(2, result.OrderCount);
        Assert.Equal(17, result.UnitsSold);
        Assert.Equal(200m, result.AverageOrderValue);
        Assert.Equal(150m, result.RevenueComparison.PreviousValue);
        Assert.Equal(166.67m, result.RevenueComparison.PercentageChange);
        Assert.Equal(ComparisonStatus.Increase, result.RevenueComparison.Status);
    }

    [PostgreSqlFact]
    public async Task Daily_weekly_and_monthly_trends_keep_year_transition_and_zero_buckets()
    {
        if (!database.IsEnabled)
        {
            return;
        }

        await using var dbContext = database.CreateContext();
        var service = new AnalyticsService(dbContext);

        var daily = await service.GetSalesTrendAsync(
            CurrentFrom,
            CurrentTo,
            AnalyticsGranularity.Daily);
        var weekly = await service.GetSalesTrendAsync(
            CurrentFrom,
            CurrentTo,
            AnalyticsGranularity.Weekly);
        var monthly = await service.GetSalesTrendAsync(
            CurrentFrom,
            CurrentTo,
            AnalyticsGranularity.Monthly);

        Assert.Equal(7, daily.Revenue.Count);
        Assert.Equal(300m, daily.Revenue.Single(
            point => point.PeriodStart == CurrentFrom).Value);
        Assert.Equal(100m, daily.Revenue.Single(
            point => point.PeriodStart == new DateTimeOffset(
                2025, 1, 2, 0, 0, 0, TimeSpan.Zero)).Value);
        Assert.Equal(5, daily.Revenue.Count(point => point.Value == 0m));
        var week = Assert.Single(weekly.Revenue);
        Assert.Equal(CurrentFrom, week.PeriodStart);
        Assert.Equal(400m, week.Value);
        Assert.Collection(
            monthly.Revenue,
            december =>
            {
                Assert.Equal(
                    new DateTimeOffset(2024, 12, 1, 0, 0, 0, TimeSpan.Zero),
                    december.PeriodStart);
                Assert.Equal(300m, december.Value);
            },
            january =>
            {
                Assert.Equal(
                    new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero),
                    january.PeriodStart);
                Assert.Equal(100m, january.Value);
            });
    }

    [PostgreSqlFact]
    public async Task Top_products_rank_differently_by_quantity_and_revenue()
    {
        if (!database.IsEnabled)
        {
            return;
        }

        await using var dbContext = database.CreateContext();
        var service = new AnalyticsService(dbContext);

        var byQuantity = await service.GetTopProductsAsync(
            CurrentFrom,
            CurrentTo,
            TopProductsMetric.Quantity,
            limit: 3);
        var byRevenue = await service.GetTopProductsAsync(
            CurrentFrom,
            CurrentTo,
            TopProductsMetric.Revenue,
            limit: 3);

        Assert.Equal(["Alpha", "Cup", "Beta"], byQuantity.Items.Select(item => item.Name));
        Assert.Equal([10L, 5L, 2L], byQuantity.Items.Select(item => item.Quantity));
        Assert.Equal(["Beta", "Alpha", "Cup"], byRevenue.Items.Select(item => item.Name));
        Assert.Equal([200m, 100m, 100m], byRevenue.Items.Select(item => item.Revenue));
    }

    [PostgreSqlFact]
    public async Task Category_share_and_previous_period_change_match_hand_calculation()
    {
        if (!database.IsEnabled)
        {
            return;
        }

        await using var dbContext = database.CreateContext();
        var service = new AnalyticsService(dbContext);

        var result = await service.GetCategoryPerformanceAsync(CurrentFrom, CurrentTo);

        Assert.Equal(400m, result.TotalRevenue);
        Assert.Equal(150m, result.TotalRevenueComparison.PreviousValue);
        Assert.Equal(166.67m, result.TotalRevenueComparison.PercentageChange);
        Assert.Collection(
            result.Items,
            electronics =>
            {
                Assert.Equal("Electronics", electronics.Name);
                Assert.Equal(300m, electronics.Revenue);
                Assert.Equal(12, electronics.UnitsSold);
                Assert.Equal(75m, electronics.RevenueSharePercentage);
                Assert.Equal(50m, electronics.RevenueComparison.PreviousValue);
                Assert.Equal(500m, electronics.RevenueComparison.PercentageChange);
            },
            home =>
            {
                Assert.Equal("Home", home.Name);
                Assert.Equal(100m, home.Revenue);
                Assert.Equal(5, home.UnitsSold);
                Assert.Equal(25m, home.RevenueSharePercentage);
                Assert.Equal(100m, home.RevenueComparison.PreviousValue);
                Assert.Equal(0m, home.RevenueComparison.PercentageChange);
            });
    }

    [PostgreSqlFact]
    public async Task Return_rate_reasons_and_most_returned_products_match_hand_calculation()
    {
        if (!database.IsEnabled)
        {
            return;
        }

        await using var dbContext = database.CreateContext();
        var service = new AnalyticsService(dbContext);

        var result = await service.GetReturnAnalyticsAsync(CurrentFrom, CurrentTo);

        Assert.Equal(3, result.ReturnRecordCount);
        Assert.Equal(6, result.ReturnedQuantity);
        Assert.Equal(70m, result.TotalReturnAmount);
        Assert.Equal(17, result.SoldQuantity);
        Assert.Equal(35.29m, result.ReturnRatePercentage);
        Assert.Equal(ReturnRateStatus.Calculated, result.ReturnRateStatus);
        Assert.Collection(
            result.Reasons,
            defective =>
            {
                Assert.Equal("Defective", defective.Reason);
                Assert.Equal(2, defective.ReturnCount);
                Assert.Equal(5, defective.Quantity);
                Assert.Equal(50m, defective.Amount);
            },
            changedMind =>
            {
                Assert.Equal("Changed mind", changedMind.Reason);
                Assert.Equal(1, changedMind.ReturnCount);
                Assert.Equal(1, changedMind.Quantity);
                Assert.Equal(20m, changedMind.Amount);
            });
        Assert.Collection(
            result.MostReturnedProducts,
            alpha =>
            {
                Assert.Equal(1, alpha.Rank);
                Assert.Equal("Alpha", alpha.Name);
                Assert.Equal(5, alpha.Quantity);
            },
            cup =>
            {
                Assert.Equal(2, cup.Rank);
                Assert.Equal("Cup", cup.Name);
                Assert.Equal(1, cup.Quantity);
            });
    }

    [PostgreSqlFact]
    public async Task Empty_period_and_zero_sales_denominator_return_defined_zero_policy()
    {
        if (!database.IsEnabled)
        {
            return;
        }

        await using var dbContext = database.CreateContext();
        var service = new AnalyticsService(dbContext);
        var emptyFrom = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var emptyTo = new DateTimeOffset(2026, 1, 4, 0, 0, 0, TimeSpan.Zero);

        var summary = await service.GetSalesSummaryAsync(emptyFrom, emptyTo);
        var trend = await service.GetSalesTrendAsync(
            emptyFrom,
            emptyTo,
            AnalyticsGranularity.Daily);
        var noBaselineReturns = await service.GetReturnAnalyticsAsync(
            new DateTimeOffset(2030, 1, 1, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2030, 1, 2, 0, 0, 0, TimeSpan.Zero));

        Assert.Equal(0m, summary.TotalRevenue);
        Assert.Equal(0, summary.OrderCount);
        Assert.Equal(0, summary.UnitsSold);
        Assert.Equal(0m, summary.AverageOrderValue);
        Assert.Equal(3, trend.Revenue.Count);
        Assert.All(trend.Revenue, point => Assert.Equal(0m, point.Value));
        Assert.Equal(1, noBaselineReturns.ReturnRecordCount);
        Assert.Equal(1, noBaselineReturns.ReturnedQuantity);
        Assert.Equal(0, noBaselineReturns.SoldQuantity);
        Assert.Null(noBaselineReturns.ReturnRatePercentage);
        Assert.Equal(
            ReturnRateStatus.NoSalesBaseline,
            noBaselineReturns.ReturnRateStatus);
    }
}
