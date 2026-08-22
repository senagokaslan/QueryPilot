namespace QueryPilot.Api.Features.Orders.Dtos;

public sealed record OrderCustomerSummaryResponse(
    long Id,
    string Name,
    string Email,
    string City);
