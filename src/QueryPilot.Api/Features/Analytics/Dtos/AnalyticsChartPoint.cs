namespace QueryPilot.Api.Features.Analytics.Dtos;

public sealed record AnalyticsChartPoint(
    DateTimeOffset PeriodStart,
    string Label,
    decimal Value);
