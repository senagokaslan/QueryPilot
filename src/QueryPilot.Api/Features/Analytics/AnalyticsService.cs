using System.Globalization;
using Microsoft.EntityFrameworkCore;
using QueryPilot.Api.Common.Exceptions;
using QueryPilot.Api.Data;
using QueryPilot.Api.Features.Analytics.Dtos;
using QueryPilot.Api.Features.Orders;

namespace QueryPilot.Api.Features.Analytics;

public sealed class AnalyticsService(AppDbContext dbContext) : IAnalyticsService
{
    public const string PreviousPeriodPolicy =
        "The previous period is adjacent to the current period and uses the exact same elapsed UTC duration; calendar month boundaries are not shifted independently.";

    public const int MaximumRangeInYears = 5;

    public const OrderStatus IncludedOrderStatus = OrderStatus.Completed;

    public const int DefaultTopProductsLimit = 5;

    public const int MaximumTopProductsLimit = 50;

    public const int PercentageDecimalPlaces = 2;

    public const int ReturnAnalyticsTopProductsLimit = 10;

    public const string ReturnRateDefinition =
        "Returned quantity whose ReturnDate is in the selected UTC range divided by units sold from completed orders whose OrderDate is in the same UTC range, multiplied by 100.";

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
        var previousRange = CreatePreviousRange(range);
        var currentMetrics = await GetSalesSummaryMetricsAsync(
            range,
            cancellationToken);
        var previousMetrics = await GetSalesSummaryMetricsAsync(
            previousRange,
            cancellationToken);

        return new SalesSummaryResponse(
            range.FromUtc,
            range.ToUtc,
            currentMetrics.TotalRevenue,
            currentMetrics.OrderCount,
            currentMetrics.UnitsSold,
            currentMetrics.AverageOrderValue,
            CreateComparisonPeriod(range, previousRange),
            CreateMetricComparison(
                currentMetrics.TotalRevenue,
                previousMetrics.TotalRevenue));
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

    public async Task<CategoryPerformanceResponse> GetCategoryPerformanceAsync(
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken = default)
    {
        var range = CreateDateRange(from, to);
        var previousRange = CreatePreviousRange(range);
        var currentMetrics = await GetCategoryMetricsAsync(
            range,
            cancellationToken);
        var previousMetrics = await GetCategoryMetricsAsync(
            previousRange,
            cancellationToken);
        var currentByCategory = currentMetrics.ToDictionary(
            category => category.CategoryId);
        var previousByCategory = previousMetrics.ToDictionary(
            category => category.CategoryId);
        var totalRevenue = currentMetrics.Sum(category => category.Revenue);
        var previousTotalRevenue = previousMetrics.Sum(category => category.Revenue);
        var categories = currentMetrics
            .Concat(previousMetrics)
            .GroupBy(category => category.CategoryId)
            .Select(group => group.First())
            .Select(category =>
            {
                var current = currentByCategory.GetValueOrDefault(category.CategoryId);
                var previous = previousByCategory.GetValueOrDefault(category.CategoryId);

                return new
                {
                    category.CategoryId,
                    category.CategoryName,
                    Revenue = current?.Revenue ?? AnalyticsResponseDefaults.Money,
                    UnitsSold = current?.UnitsSold ?? AnalyticsResponseDefaults.Quantity,
                    PreviousRevenue = previous?.Revenue ?? AnalyticsResponseDefaults.Money
                };
            })
            .OrderByDescending(category => category.Revenue)
            .ThenBy(category => category.CategoryId)
            .Select((category, index) => new CategoryPerformanceItemResponse(
                index + 1,
                category.CategoryId,
                category.CategoryName,
                category.Revenue,
                category.UnitsSold,
                totalRevenue == AnalyticsResponseDefaults.Money
                    ? AnalyticsResponseDefaults.Percentage
                    : category.Revenue / totalRevenue * 100m,
                CreateMetricComparison(
                    category.Revenue,
                    category.PreviousRevenue)))
            .ToArray();

        return new CategoryPerformanceResponse(
            range.FromUtc,
            range.ToUtc,
            totalRevenue,
            categories,
            CreateComparisonPeriod(range, previousRange),
            CreateMetricComparison(totalRevenue, previousTotalRevenue));
    }

    public async Task<ReturnAnalyticsResponse> GetReturnAnalyticsAsync(
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken = default)
    {
        var range = CreateDateRange(from, to);
        var fromUtc = range.FromUtc.UtcDateTime;
        var toUtc = range.ToUtc.UtcDateTime;
        var returnRecords = dbContext.Returns
            .AsNoTracking()
            .Where(returnRecord =>
                returnRecord.ReturnDate >= fromUtc
                && returnRecord.ReturnDate < toUtc);
        var returnSummary = await returnRecords
            .GroupBy(_ => 1)
            .Select(group => new
            {
                ReturnRecordCount = group.Count(),
                ReturnedQuantity = group.Sum(returnRecord => (long)returnRecord.Quantity),
                TotalReturnAmount = group.Sum(returnRecord => returnRecord.Amount)
            })
            .SingleOrDefaultAsync(cancellationToken);
        var soldQuantity = await dbContext.OrderItems
            .AsNoTracking()
            .Where(item =>
                item.Order.Status == IncludedOrderStatus
                && item.Order.OrderDate >= fromUtc
                && item.Order.OrderDate < toUtc)
            .SumAsync(
                item => (long?)item.Quantity,
                cancellationToken)
            ?? AnalyticsResponseDefaults.Quantity;
        var reasonMetrics = await returnRecords
            .GroupBy(returnRecord => returnRecord.Reason)
            .Select(group => new
            {
                Reason = group.Key,
                ReturnCount = group.Count(),
                Quantity = group.Sum(returnRecord => (long)returnRecord.Quantity),
                Amount = group.Sum(returnRecord => returnRecord.Amount)
            })
            .OrderByDescending(reason => reason.Quantity)
            .ThenByDescending(reason => reason.ReturnCount)
            .ThenBy(reason => reason.Reason)
            .ToListAsync(cancellationToken);
        var productMetrics = await returnRecords
            .GroupBy(returnRecord => new
            {
                returnRecord.OrderItem.ProductId,
                returnRecord.OrderItem.Product.Name,
                returnRecord.OrderItem.Product.SKU
            })
            .Select(group => new
            {
                group.Key.ProductId,
                group.Key.Name,
                group.Key.SKU,
                ReturnRecordCount = group.Count(),
                Quantity = group.Sum(returnRecord => (long)returnRecord.Quantity),
                Amount = group.Sum(returnRecord => returnRecord.Amount)
            })
            .OrderByDescending(product => product.Quantity)
            .ThenByDescending(product => product.Amount)
            .ThenBy(product => product.ProductId)
            .Take(ReturnAnalyticsTopProductsLimit)
            .ToListAsync(cancellationToken);
        var returnedQuantity = returnSummary?.ReturnedQuantity
            ?? AnalyticsResponseDefaults.Quantity;
        decimal? returnRatePercentage = soldQuantity == AnalyticsResponseDefaults.Quantity
            ? null
            : Math.Round(
                (decimal)returnedQuantity / soldQuantity * 100m,
                PercentageDecimalPlaces,
                MidpointRounding.AwayFromZero);
        var mostReturnedProducts = productMetrics
            .Select((product, index) => new ReturnedProductResponse(
                index + 1,
                product.ProductId,
                product.Name,
                product.SKU,
                product.ReturnRecordCount,
                product.Quantity,
                product.Amount))
            .ToArray();
        var reasons = reasonMetrics
            .Select(reason => new ReturnReasonBreakdownResponse(
                reason.Reason,
                reason.ReturnCount,
                reason.Quantity,
                reason.Amount))
            .ToArray();

        return new ReturnAnalyticsResponse(
            range.FromUtc,
            range.ToUtc,
            returnSummary?.ReturnRecordCount ?? AnalyticsResponseDefaults.Quantity,
            returnedQuantity,
            returnSummary?.TotalReturnAmount ?? AnalyticsResponseDefaults.Money,
            soldQuantity,
            returnRatePercentage,
            soldQuantity == AnalyticsResponseDefaults.Quantity
                ? ReturnRateStatus.NoSalesBaseline
                : ReturnRateStatus.Calculated,
            reasons,
            mostReturnedProducts,
            new ReturnAnalyticsMetadataResponse(
                ReturnRateDefinition,
                "Return metrics use ReturnDate with an inclusive start and exclusive end.",
                "The sales denominator uses OrderDate for completed orders with the same inclusive-start, exclusive-end range.",
                ReturnAnalyticsTopProductsLimit));
    }

    private async Task<SalesSummaryMetrics> GetSalesSummaryMetricsAsync(
        AnalyticsDateRange range,
        CancellationToken cancellationToken)
    {
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
            return SalesSummaryMetrics.Empty;
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

        return new SalesSummaryMetrics(
            orderMetrics.TotalRevenue,
            orderMetrics.OrderCount,
            unitsSold,
            orderMetrics.TotalRevenue / orderMetrics.OrderCount);
    }

    private async Task<IReadOnlyList<CategorySalesMetric>> GetCategoryMetricsAsync(
        AnalyticsDateRange range,
        CancellationToken cancellationToken)
    {
        var fromUtc = range.FromUtc.UtcDateTime;
        var toUtc = range.ToUtc.UtcDateTime;
        var metrics = await dbContext.OrderItems
            .AsNoTracking()
            .Where(item =>
                item.Order.Status == IncludedOrderStatus
                && item.Order.OrderDate >= fromUtc
                && item.Order.OrderDate < toUtc)
            .GroupBy(item => new
            {
                item.Product.CategoryId,
                CategoryName = item.Product.Category.Name
            })
            .Select(group => new
            {
                group.Key.CategoryId,
                group.Key.CategoryName,
                Revenue = group.Sum(item => item.LineTotal),
                UnitsSold = group.Sum(item => (long)item.Quantity)
            })
            .ToListAsync(cancellationToken);

        return metrics.Select(metric => new CategorySalesMetric(
            metric.CategoryId,
            metric.CategoryName,
            metric.Revenue,
            metric.UnitsSold)).ToArray();
    }

    private static AnalyticsDateRange CreatePreviousRange(
        AnalyticsDateRange currentRange)
    {
        try
        {
            return new AnalyticsDateRange(
                currentRange.FromUtc - currentRange.Duration,
                currentRange.FromUtc);
        }
        catch (ArgumentOutOfRangeException)
        {
            throw new RequestValidationException(
                new Dictionary<string, string[]>
                {
                    ["from"] =
                    ["The selected range does not leave room for an equally long previous period."]
                });
        }
    }

    private static ComparisonPeriodResponse CreateComparisonPeriod(
        AnalyticsDateRange currentRange,
        AnalyticsDateRange previousRange) =>
        new(
            currentRange.FromUtc,
            currentRange.ToUtc,
            previousRange.FromUtc,
            previousRange.ToUtc,
            currentRange.Duration.Ticks,
            PreviousPeriodPolicy);

    private static MetricComparisonResponse CreateMetricComparison(
        decimal currentValue,
        decimal previousValue)
    {
        if (previousValue == AnalyticsResponseDefaults.Money)
        {
            return new MetricComparisonResponse(
                currentValue,
                previousValue,
                null,
                ComparisonStatus.NoBaseline);
        }

        var percentageChange = Math.Round(
            (currentValue - previousValue) / previousValue * 100m,
            PercentageDecimalPlaces,
            MidpointRounding.AwayFromZero);
        var status = currentValue.CompareTo(previousValue) switch
        {
            > 0 => ComparisonStatus.Increase,
            < 0 => ComparisonStatus.Decrease,
            _ => ComparisonStatus.NoChange
        };

        return new MetricComparisonResponse(
            currentValue,
            previousValue,
            percentageChange,
            status);
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

    private sealed record SalesSummaryMetrics(
        decimal TotalRevenue,
        int OrderCount,
        long UnitsSold,
        decimal AverageOrderValue)
    {
        public static SalesSummaryMetrics Empty { get; } = new(
            AnalyticsResponseDefaults.Money,
            AnalyticsResponseDefaults.Quantity,
            AnalyticsResponseDefaults.Quantity,
            AnalyticsResponseDefaults.Money);
    }

    private sealed record CategorySalesMetric(
        long CategoryId,
        string CategoryName,
        decimal Revenue,
        long UnitsSold);
}
