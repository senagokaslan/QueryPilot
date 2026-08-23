namespace QueryPilot.Api.Features.Analytics.Dtos;

public sealed record SalesSummaryResponse(
    DateTimeOffset FromUtc,
    DateTimeOffset ToUtc,
    decimal TotalRevenue,
    int OrderCount,
    long UnitsSold,
    decimal AverageOrderValue,
    ComparisonPeriodResponse ComparisonPeriod,
    MetricComparisonResponse RevenueComparison);
