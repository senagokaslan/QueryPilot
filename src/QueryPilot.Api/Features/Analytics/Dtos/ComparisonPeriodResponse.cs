namespace QueryPilot.Api.Features.Analytics.Dtos;

public sealed record ComparisonPeriodResponse(
    DateTimeOffset CurrentFromUtc,
    DateTimeOffset CurrentToUtc,
    DateTimeOffset PreviousFromUtc,
    DateTimeOffset PreviousToUtc,
    long DurationTicks,
    string Policy);
