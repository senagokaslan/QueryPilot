namespace QueryPilot.Api.Features.Analytics.Dtos;

public sealed record TopProductResponse(
    int Rank,
    long ProductId,
    string Name,
    string SKU,
    long Quantity,
    decimal Revenue);
