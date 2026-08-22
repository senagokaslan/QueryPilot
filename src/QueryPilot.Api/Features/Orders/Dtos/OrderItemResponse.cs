namespace QueryPilot.Api.Features.Orders.Dtos;

public sealed record OrderItemResponse(
    long Id,
    OrderProductSummaryResponse Product,
    int Quantity,
    decimal UnitPrice,
    decimal LineTotal);
