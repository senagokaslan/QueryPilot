namespace QueryPilot.Api.Features.Analytics.Dtos;

public sealed record MetricComparisonResponse(
    decimal CurrentValue,
    decimal PreviousValue,
    decimal? PercentageChange,
    ComparisonStatus Status);
