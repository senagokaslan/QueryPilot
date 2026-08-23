namespace QueryPilot.Api.Features.Ai;

public sealed record AiClarificationField(
    string Field,
    string Question,
    IReadOnlyList<string> AllowedValues);
