namespace QueryPilot.Api.Features.Ai;

public sealed record AiIntentEvaluationResult(
    ValidatedAnalyticsIntent? Intent,
    IReadOnlyList<AiClarificationField> RequiredFields)
{
    public bool NeedsClarification => RequiredFields.Count > 0;

    public static AiIntentEvaluationResult Valid(ValidatedAnalyticsIntent intent) =>
        new(intent, []);

    public static AiIntentEvaluationResult Clarify(
        IReadOnlyList<AiClarificationField> requiredFields) =>
        new(null, requiredFields);
}
