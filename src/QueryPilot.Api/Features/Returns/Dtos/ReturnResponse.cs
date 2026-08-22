namespace QueryPilot.Api.Features.Returns.Dtos;

public sealed record ReturnResponse(
    long Id,
    long OrderItemId,
    int Quantity,
    string Reason,
    DateTime ReturnDate,
    decimal Amount,
    int RemainingReturnableQuantity);
