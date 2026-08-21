namespace QueryPilot.Api.Features.Products.Dtos;

public sealed record ProductCategorySummaryResponse(
    long Id,
    string Name,
    bool IsActive);
