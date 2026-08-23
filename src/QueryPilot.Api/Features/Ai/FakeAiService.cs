namespace QueryPilot.Api.Features.Ai;

public sealed class FakeAiService(
    Func<string, CancellationToken, Task<AiQuestionUnderstanding>> understand,
    Func<string, string, CancellationToken, Task<AiResultExplanation>> explain)
    : IAiService
{
    public FakeAiService(
        AiQuestionUnderstanding understanding,
        AiResultExplanation explanation)
        : this(
            (_, _) => Task.FromResult(understanding),
            (_, _, _) => Task.FromResult(explanation))
    {
    }

    public Task<AiQuestionUnderstanding> UnderstandQuestionAsync(
        string question,
        CancellationToken cancellationToken = default) =>
        understand(question, cancellationToken);

    public Task<AiResultExplanation> ExplainResultAsync(
        string question,
        string analyticsResultJson,
        CancellationToken cancellationToken = default) =>
        explain(question, analyticsResultJson, cancellationToken);
}
