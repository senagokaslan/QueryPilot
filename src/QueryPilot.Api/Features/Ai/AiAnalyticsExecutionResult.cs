namespace QueryPilot.Api.Features.Ai;

public sealed record AiAnalyticsExecutionResult(
    ValidatedAnalyticsIntent Intent,
    object Data);
