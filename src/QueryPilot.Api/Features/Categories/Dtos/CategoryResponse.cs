namespace QueryPilot.Api.Features.Categories.Dtos;

public sealed record CategoryResponse(
    long Id,
    string Name,
    bool IsActive,
    DateTime CreatedAt);
