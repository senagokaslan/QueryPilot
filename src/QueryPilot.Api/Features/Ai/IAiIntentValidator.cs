namespace QueryPilot.Api.Features.Ai;

public interface IAiIntentValidator
{
    Task<ValidatedAnalyticsIntent> ValidateAsync(
        AiQuestionUnderstanding intent,
        CancellationToken cancellationToken = default);
}
