namespace QueryPilot.Api.Features.Ai;

public sealed record AiExplanationResponse(
    AiExplanationStatus Status,
    string? Text)
{
    public static AiExplanationResponse Available(string text) =>
        new(AiExplanationStatus.Available, text);

    public static AiExplanationResponse Unavailable() =>
        new(AiExplanationStatus.Unavailable, Text: null);

    public static AiExplanationResponse RejectedUnsafe() =>
        new(AiExplanationStatus.RejectedUnsafe, Text: null);
}
