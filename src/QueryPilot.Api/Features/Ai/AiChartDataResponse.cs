using QueryPilot.Api.Features.Analytics.Dtos;

namespace QueryPilot.Api.Features.Ai;

/// <summary>Chart-ready time-series data when the selected analysis produces a trend.</summary>
public sealed record AiChartDataResponse(
    IReadOnlyList<AnalyticsChartPoint> Revenue,
    IReadOnlyList<AnalyticsChartPoint> OrderCount,
    IReadOnlyList<AnalyticsChartPoint> UnitsSold);
