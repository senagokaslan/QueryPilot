namespace QueryPilot.Api.Features.Orders.Dtos;

public sealed record OrderListResponse(
    long Id,
    long CustomerId,
    DateTime OrderDate,
    OrderStatus Status,
    decimal TotalAmount,
    int ItemCount);
