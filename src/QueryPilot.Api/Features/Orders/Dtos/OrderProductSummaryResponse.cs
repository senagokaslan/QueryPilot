namespace QueryPilot.Api.Features.Orders.Dtos;

public sealed record OrderProductSummaryResponse(
    long Id,
    string Name,
    string SKU);
