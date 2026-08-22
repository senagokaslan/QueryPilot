using Microsoft.AspNetCore.Mvc;
using QueryPilot.Api.Features.Returns.Dtos;

namespace QueryPilot.Api.Features.Returns;

[ApiController]
[Route("api/returns")]
public sealed class ReturnsController(IReturnService returnService) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType<ReturnResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ReturnResponse>> Create(
        CreateReturnRequest request,
        CancellationToken cancellationToken)
    {
        var returnRecord = await returnService.CreateAsync(request, cancellationToken);

        return StatusCode(StatusCodes.Status201Created, returnRecord);
    }
}
