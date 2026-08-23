namespace QueryPilot.Api.Features.Analytics.Dtos;

public sealed record CategoryPerformanceItemResponse(
    int Rank,
    long CategoryId,
    string Name,
    decimal Revenue,
    long UnitsSold,
    decimal RevenueSharePercentage);
