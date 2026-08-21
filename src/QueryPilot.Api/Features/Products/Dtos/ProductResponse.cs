namespace QueryPilot.Api.Features.Products.Dtos;

public sealed record ProductResponse(
    long Id,
    string Name,
    string SKU,
    ProductCategorySummaryResponse Category,
    decimal UnitPrice,
    bool IsActive,
    DateTime CreatedAt);
