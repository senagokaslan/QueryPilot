using Microsoft.AspNetCore.Mvc;

namespace QueryPilot.Api.Features.Ai;

/// <summary>Natural-language access to QueryPilot's supported BI analytics.</summary>
[ApiController]
[Route("api/ai")]
public sealed class AiController(IAiAnalyticsCoordinator coordinator) : ControllerBase
{
    /// <summary>Understands, validates and executes a supported analytics question.</summary>
    /// <remarks>
    /// Returns structured backend analytics data with an optional grounded Turkish
    /// explanation. Ambiguous questions return NeedsClarification; BI questions outside
    /// the supported scope return Unsupported. Provider failures return Problem Details 503.
    /// </remarks>
    [HttpPost("query")]
    [Produces("application/json")]
    [ProducesResponseType<AiAnalyticsExecutionResult>(StatusCodes.Status200OK)]
    [ProducesResponseType<HttpValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<AiAnalyticsExecutionResult>> Query(
        AiQueryRequest request,
        CancellationToken cancellationToken)
    {
        var result = await coordinator.ExecuteAsync(
            request.Question.Trim(),
            cancellationToken);

        return Ok(result);
    }
}
