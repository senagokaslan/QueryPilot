namespace QueryPilot.Api.Features.Analytics.Dtos;

public sealed record CategoryPerformanceResponse(
    DateTimeOffset FromUtc,
    DateTimeOffset ToUtc,
    decimal TotalRevenue,
    IReadOnlyList<CategoryPerformanceItemResponse> Items);
