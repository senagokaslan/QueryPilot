namespace QueryPilot.Api.Features.Ai;

public interface IAiService
{
    Task<AiQuestionUnderstanding> UnderstandQuestionAsync(
        string question,
        CancellationToken cancellationToken = default);

    Task<AiResultExplanation> ExplainResultAsync(
        string question,
        string analyticsResultJson,
        CancellationToken cancellationToken = default);
}
