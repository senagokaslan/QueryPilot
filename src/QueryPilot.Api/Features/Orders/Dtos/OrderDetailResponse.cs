namespace QueryPilot.Api.Features.Orders.Dtos;

public sealed record OrderDetailResponse(
    long Id,
    OrderCustomerSummaryResponse Customer,
    DateTime OrderDate,
    OrderStatus Status,
    decimal TotalAmount,
    IReadOnlyList<OrderItemResponse> Items);
