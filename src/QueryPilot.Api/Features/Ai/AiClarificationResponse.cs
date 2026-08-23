namespace QueryPilot.Api.Features.Ai;

public sealed record AiClarificationResponse(
    string OriginalQuestion,
    AiQuestionUnderstanding CurrentIntent,
    IReadOnlyList<AiClarificationField> RequiredFields,
    string RetryInstruction);
