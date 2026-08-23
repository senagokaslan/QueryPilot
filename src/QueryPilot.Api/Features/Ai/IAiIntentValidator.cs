namespace QueryPilot.Api.Features.Ai;

public interface IAiIntentValidator
{
    Task<AiIntentEvaluationResult> EvaluateAsync(
        AiQuestionUnderstanding intent,
        CancellationToken cancellationToken = default);

    Task<ValidatedAnalyticsIntent> ValidateAsync(
        AiQuestionUnderstanding intent,
        CancellationToken cancellationToken = default);
}
