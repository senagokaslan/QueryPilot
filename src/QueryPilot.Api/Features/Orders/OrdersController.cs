using Microsoft.AspNetCore.Mvc;
using QueryPilot.Api.Common.Pagination;
using QueryPilot.Api.Features.Orders.Dtos;

namespace QueryPilot.Api.Features.Orders;

[ApiController]
[Route("api/orders")]
public sealed class OrdersController(IOrderService orderService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<PagedResponse<OrderListResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedResponse<OrderListResponse>>> GetAll(
        [FromQuery] OrderListRequest request,
        CancellationToken cancellationToken)
    {
        return Ok(await orderService.GetAllAsync(request, cancellationToken));
    }

    [HttpGet("{id:long}")]
    [ProducesResponseType<OrderDetailResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OrderDetailResponse>> GetById(
        long id,
        CancellationToken cancellationToken)
    {
        return Ok(await orderService.GetByIdAsync(id, cancellationToken));
    }

    [HttpPost]
    [ProducesResponseType<OrderDetailResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<OrderDetailResponse>> Create(
        CreateOrderRequest request,
        CancellationToken cancellationToken)
    {
        var order = await orderService.CreateAsync(request, cancellationToken);

        return CreatedAtAction(nameof(GetById), new { id = order.Id }, order);
    }
}
