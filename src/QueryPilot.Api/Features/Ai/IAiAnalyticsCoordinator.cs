namespace QueryPilot.Api.Features.Ai;

public interface IAiAnalyticsCoordinator
{
    Task<AiAnalyticsExecutionResult> ExecuteAsync(
        string question,
        CancellationToken cancellationToken = default);
}
