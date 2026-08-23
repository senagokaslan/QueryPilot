namespace QueryPilot.Api.Features.Analytics.Dtos;

public sealed record SalesSummaryResponse(
    DateTimeOffset FromUtc,
    DateTimeOffset ToUtc,
    decimal TotalRevenue,
    int OrderCount,
    long UnitsSold,
    decimal AverageOrderValue)
{
    public static SalesSummaryResponse Empty(AnalyticsDateRange range) =>
        new(
            range.FromUtc,
            range.ToUtc,
            AnalyticsResponseDefaults.Money,
            AnalyticsResponseDefaults.Quantity,
            AnalyticsResponseDefaults.Quantity,
            AnalyticsResponseDefaults.Money);
}
