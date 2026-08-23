using System.Globalization;
using Microsoft.EntityFrameworkCore;
using QueryPilot.Api.Common.Exceptions;
using QueryPilot.Api.Data;
using QueryPilot.Api.Features.Analytics.Dtos;
using QueryPilot.Api.Features.Orders;

namespace QueryPilot.Api.Features.Analytics;

public sealed class AnalyticsService(AppDbContext dbContext) : IAnalyticsService
{
    public const int MaximumRangeInYears = 5;

    public const OrderStatus IncludedOrderStatus = OrderStatus.Completed;

    public const int DefaultTopProductsLimit = 5;

    public const int MaximumTopProductsLimit = 50;

    public AnalyticsDateRange CreateDateRange(
        DateTimeOffset from,
        DateTimeOffset to)
    {
        var fromUtc = from.ToUniversalTime();
        var toUtc = to.ToUniversalTime();
        var errors = new Dictionary<string, string[]>();

        if (fromUtc >= toUtc)
        {
            errors[nameof(from)] =
                ["From must be earlier than To."];
            errors[nameof(to)] =
                ["To must be later than From."];
        }
        else if (ExceedsMaximumRange(fromUtc, toUtc))
        {
            errors[nameof(to)] =
                [$"The analytics date range cannot exceed {MaximumRangeInYears} years."];
        }

        if (errors.Count > 0)
        {
            throw new RequestValidationException(errors);
        }

        return new AnalyticsDateRange(fromUtc, toUtc);
    }

    public async Task<SalesSummaryResponse> GetSalesSummaryAsync(
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken = default)
    {
        var range = CreateDateRange(from, to);
        var fromUtc = range.FromUtc.UtcDateTime;
        var toUtc = range.ToUtc.UtcDateTime;
        var completedOrders = dbContext.Orders
            .AsNoTracking()
            .Where(order =>
                order.Status == IncludedOrderStatus
                && order.OrderDate >= fromUtc
                && order.OrderDate < toUtc);

        var orderMetrics = await completedOrders
            .GroupBy(_ => 1)
            .Select(group => new
            {
                TotalRevenue = group.Sum(order => order.TotalAmount),
                OrderCount = group.Count()
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (orderMetrics is null)
        {
            return SalesSummaryResponse.Empty(range);
        }

        var unitsSold = await dbContext.OrderItems
            .AsNoTracking()
            .Where(item =>
                item.Order.Status == IncludedOrderStatus
                && item.Order.OrderDate >= fromUtc
                && item.Order.OrderDate < toUtc)
            .SumAsync(
                item => (long?)item.Quantity,
                cancellationToken)
            ?? AnalyticsResponseDefaults.Quantity;
        var averageOrderValue =
            orderMetrics.TotalRevenue / orderMetrics.OrderCount;

        return new SalesSummaryResponse(
            range.FromUtc,
            range.ToUtc,
            orderMetrics.TotalRevenue,
            orderMetrics.OrderCount,
            unitsSold,
            averageOrderValue);
    }

    public async Task<SalesTrendResponse> GetSalesTrendAsync(
        DateTimeOffset from,
        DateTimeOffset to,
        AnalyticsGranularity granularity,
        CancellationToken cancellationToken = default)
    {
        ValidateGranularity(granularity);

        var range = CreateDateRange(from, to);
        var fromUtc = range.FromUtc.UtcDateTime;
        var toUtc = range.ToUtc.UtcDateTime;
        // PostgreSQL returns at most one aggregate row per UTC day. Weekly and
        // monthly rebucketing therefore never materializes individual orders.
        var dailyOrderMetrics = await dbContext.Orders
            .AsNoTracking()
            .Where(order =>
                order.Status == IncludedOrderStatus
                && order.OrderDate >= fromUtc
                && order.OrderDate < toUtc)
            .GroupBy(order => order.OrderDate.Date)
            .Select(group => new
            {
                Day = group.Key,
                Revenue = group.Sum(order => order.TotalAmount),
                OrderCount = group.Count()
            })
            .ToListAsync(cancellationToken);
        var dailyUnits = await dbContext.OrderItems
            .AsNoTracking()
            .Where(item =>
                item.Order.Status == IncludedOrderStatus
                && item.Order.OrderDate >= fromUtc
                && item.Order.OrderDate < toUtc)
            .GroupBy(item => item.Order.OrderDate.Date)
            .Select(group => new
            {
                Day = group.Key,
                UnitsSold = group.Sum(item => (long)item.Quantity)
            })
            .ToDictionaryAsync(
                item => item.Day,
                item => item.UnitsSold,
                cancellationToken);

        var metricsByPeriod = dailyOrderMetrics
            .Select(metric => new DailySalesMetric(
                AsUtc(metric.Day),
                metric.Revenue,
                metric.OrderCount,
                dailyUnits.GetValueOrDefault(metric.Day)))
            .GroupBy(metric => GetPeriodStart(metric.Day, granularity))
            .ToDictionary(
                group => group.Key,
                group => new PeriodSalesMetric(
                    group.Sum(metric => metric.Revenue),
                    group.Sum(metric => metric.OrderCount),
                    group.Sum(metric => metric.UnitsSold)));
        var periodStarts = EnumeratePeriodStarts(range, granularity);

        return new SalesTrendResponse(
            range.FromUtc,
            range.ToUtc,
            granularity,
            periodStarts.Select(periodStart => new AnalyticsChartPoint(
                periodStart,
                CreatePeriodLabel(periodStart, granularity),
                metricsByPeriod.GetValueOrDefault(periodStart)?.Revenue
                    ?? AnalyticsResponseDefaults.Money)).ToArray(),
            periodStarts.Select(periodStart => new AnalyticsChartPoint(
                periodStart,
                CreatePeriodLabel(periodStart, granularity),
                metricsByPeriod.GetValueOrDefault(periodStart)?.OrderCount
                    ?? AnalyticsResponseDefaults.Quantity)).ToArray(),
            periodStarts.Select(periodStart => new AnalyticsChartPoint(
                periodStart,
                CreatePeriodLabel(periodStart, granularity),
                metricsByPeriod.GetValueOrDefault(periodStart)?.UnitsSold
                    ?? AnalyticsResponseDefaults.Quantity)).ToArray());
    }

    public async Task<TopProductsResponse> GetTopProductsAsync(
        DateTimeOffset from,
        DateTimeOffset to,
        TopProductsMetric metric,
        int? limit = null,
        long? categoryId = null,
        CancellationToken cancellationToken = default)
    {
        var normalizedLimit = ValidateTopProductsParameters(
            metric,
            limit,
            categoryId);
        var range = CreateDateRange(from, to);
        var fromUtc = range.FromUtc.UtcDateTime;
        var toUtc = range.ToUtc.UtcDateTime;
        var orderItems = dbContext.OrderItems
            .AsNoTracking()
            .Where(item =>
                item.Order.Status == IncludedOrderStatus
                && item.Order.OrderDate >= fromUtc
                && item.Order.OrderDate < toUtc);

        if (categoryId.HasValue)
        {
            orderItems = orderItems.Where(item =>
                item.Product.CategoryId == categoryId.Value);
        }

        var groupedProducts = orderItems
            .GroupBy(item => new
            {
                item.ProductId,
                item.Product.Name,
                item.Product.SKU
            })
            .Select(group => new
            {
                group.Key.ProductId,
                group.Key.Name,
                group.Key.SKU,
                Quantity = group.Sum(item => (long)item.Quantity),
                Revenue = group.Sum(item => item.LineTotal)
            });
        var orderedProducts = metric == TopProductsMetric.Quantity
            ? groupedProducts
                .OrderByDescending(product => product.Quantity)
                .ThenByDescending(product => product.Revenue)
                .ThenBy(product => product.ProductId)
            : groupedProducts
                .OrderByDescending(product => product.Revenue)
                .ThenByDescending(product => product.Quantity)
                .ThenBy(product => product.ProductId);
        var products = await orderedProducts
            .Take(normalizedLimit)
            .ToListAsync(cancellationToken);
        var rankedProducts = products
            .Select((product, index) => new TopProductResponse(
                index + 1,
                product.ProductId,
                product.Name,
                product.SKU,
                product.Quantity,
                product.Revenue))
            .ToArray();

        return new TopProductsResponse(
            range.FromUtc,
            range.ToUtc,
            metric,
            normalizedLimit,
            categoryId,
            rankedProducts);
    }

    private static void ValidateGranularity(AnalyticsGranularity granularity)
    {
        if (!Enum.IsDefined(granularity))
        {
            throw new RequestValidationException(
                new Dictionary<string, string[]>
                {
                    [nameof(granularity)] =
                    ["Granularity must be Daily, Weekly, or Monthly."]
                });
        }
    }

    private static int ValidateTopProductsParameters(
        TopProductsMetric metric,
        int? limit,
        long? categoryId)
    {
        var errors = new Dictionary<string, string[]>();

        if (!Enum.IsDefined(metric))
        {
            errors[nameof(metric)] =
                ["Metric must be Quantity or Revenue."];
        }

        var normalizedLimit = limit ?? DefaultTopProductsLimit;

        if (normalizedLimit <= 0 || normalizedLimit > MaximumTopProductsLimit)
        {
            errors[nameof(limit)] =
                [$"Limit must be between 1 and {MaximumTopProductsLimit}."];
        }

        if (categoryId <= 0)
        {
            errors[nameof(categoryId)] =
                ["CategoryId must be greater than zero when provided."];
        }

        if (errors.Count > 0)
        {
            throw new RequestValidationException(errors);
        }

        return normalizedLimit;
    }

    private static DateTimeOffset AsUtc(DateTime value) =>
        new(DateTime.SpecifyKind(value, DateTimeKind.Utc));

    private static DateTimeOffset GetPeriodStart(
        DateTimeOffset value,
        AnalyticsGranularity granularity)
    {
        var dayStart = new DateTimeOffset(
            value.Year,
            value.Month,
            value.Day,
            0,
            0,
            0,
            TimeSpan.Zero);

        return granularity switch
        {
            AnalyticsGranularity.Daily => dayStart,
            AnalyticsGranularity.Weekly => dayStart.AddDays(
                -(((int)dayStart.DayOfWeek + 6) % 7)),
            AnalyticsGranularity.Monthly => new DateTimeOffset(
                dayStart.Year,
                dayStart.Month,
                1,
                0,
                0,
                0,
                TimeSpan.Zero),
            _ => throw new ArgumentOutOfRangeException(nameof(granularity))
        };
    }

    private static IReadOnlyList<DateTimeOffset> EnumeratePeriodStarts(
        AnalyticsDateRange range,
        AnalyticsGranularity granularity)
    {
        var periodStarts = new List<DateTimeOffset>();
        var current = GetPeriodStart(range.FromUtc, granularity);

        while (current < range.ToUtc)
        {
            periodStarts.Add(current);
            current = granularity switch
            {
                AnalyticsGranularity.Daily => current.AddDays(1),
                AnalyticsGranularity.Weekly => current.AddDays(7),
                AnalyticsGranularity.Monthly => current.AddMonths(1),
                _ => throw new ArgumentOutOfRangeException(nameof(granularity))
            };
        }

        return periodStarts;
    }

    private static string CreatePeriodLabel(
        DateTimeOffset periodStart,
        AnalyticsGranularity granularity) =>
        granularity switch
        {
            AnalyticsGranularity.Daily =>
                periodStart.ToString("dd MMM yyyy", CultureInfo.InvariantCulture),
            AnalyticsGranularity.Weekly =>
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"{periodStart:dd MMM yyyy} - {periodStart.AddDays(6):dd MMM yyyy}"),
            AnalyticsGranularity.Monthly =>
                periodStart.ToString("MMMM yyyy", CultureInfo.InvariantCulture),
            _ => throw new ArgumentOutOfRangeException(nameof(granularity))
        };

    private static bool ExceedsMaximumRange(
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc)
    {
        try
        {
            return toUtc > fromUtc.AddYears(MaximumRangeInYears);
        }
        catch (ArgumentOutOfRangeException)
        {
            return false;
        }
    }

    private sealed record DailySalesMetric(
        DateTimeOffset Day,
        decimal Revenue,
        int OrderCount,
        long UnitsSold);

    private sealed record PeriodSalesMetric(
        decimal Revenue,
        int OrderCount,
        long UnitsSold);
}
