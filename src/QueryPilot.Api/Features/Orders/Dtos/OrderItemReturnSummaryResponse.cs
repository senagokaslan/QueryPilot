namespace QueryPilot.Api.Features.Orders.Dtos;

public sealed record OrderItemReturnSummaryResponse(
    int ReturnCount,
    int ReturnedQuantity,
    decimal ReturnedAmount);
