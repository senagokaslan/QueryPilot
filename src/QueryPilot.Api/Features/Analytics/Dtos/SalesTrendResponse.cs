namespace QueryPilot.Api.Features.Analytics.Dtos;

public sealed record SalesTrendResponse(
    DateTimeOffset FromUtc,
    DateTimeOffset ToUtc,
    AnalyticsGranularity Granularity,
    IReadOnlyList<AnalyticsChartPoint> Revenue,
    IReadOnlyList<AnalyticsChartPoint> OrderCount,
    IReadOnlyList<AnalyticsChartPoint> UnitsSold);
