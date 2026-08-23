namespace QueryPilot.Api.Features.Analytics.Dtos;

public sealed record ReturnedProductResponse(
    int Rank,
    long ProductId,
    string Name,
    string SKU,
    int ReturnCount,
    long Quantity,
    decimal Amount);
