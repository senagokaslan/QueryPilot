namespace QueryPilot.Api.Features.Analytics.Dtos;

public sealed record TopProductsResponse(
    DateTimeOffset FromUtc,
    DateTimeOffset ToUtc,
    TopProductsMetric Metric,
    int Limit,
    long? CategoryId,
    IReadOnlyList<TopProductResponse> Items);
