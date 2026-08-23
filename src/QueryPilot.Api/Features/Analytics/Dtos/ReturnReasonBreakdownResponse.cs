namespace QueryPilot.Api.Features.Analytics.Dtos;

public sealed record ReturnReasonBreakdownResponse(
    string Reason,
    int ReturnCount,
    long Quantity,
    decimal Amount);
