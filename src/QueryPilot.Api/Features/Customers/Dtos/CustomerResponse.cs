namespace QueryPilot.Api.Features.Customers.Dtos;

public sealed record CustomerResponse(
    long Id,
    string Name,
    string Email,
    string City,
    bool IsActive,
    DateTime CreatedAt);
