using Microsoft.AspNetCore.Mvc;
using QueryPilot.Api.Features.Analytics.Dtos;

namespace QueryPilot.Api.Features.Analytics;

[ApiController]
[Route("api/analytics")]
public sealed class AnalyticsController(IAnalyticsService analyticsService)
    : ControllerBase
{
    [HttpGet("summary")]
    [ProducesResponseType<SalesSummaryResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<SalesSummaryResponse>> GetSummary(
        [FromQuery] AnalyticsDateRangeRequest request,
        CancellationToken cancellationToken)
    {
        return Ok(await analyticsService.GetSalesSummaryAsync(
            request.From!.Value,
            request.To!.Value,
            cancellationToken));
    }

    [HttpGet("sales-trend")]
    [ProducesResponseType<SalesTrendResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<SalesTrendResponse>> GetSalesTrend(
        [FromQuery] SalesTrendRequest request,
        CancellationToken cancellationToken)
    {
        return Ok(await analyticsService.GetSalesTrendAsync(
            request.From!.Value,
            request.To!.Value,
            request.Granularity!.Value,
            cancellationToken));
    }

    [HttpGet("top-products")]
    [ProducesResponseType<TopProductsResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<TopProductsResponse>> GetTopProducts(
        [FromQuery] TopProductsRequest request,
        CancellationToken cancellationToken)
    {
        return Ok(await analyticsService.GetTopProductsAsync(
            request.From!.Value,
            request.To!.Value,
            request.Metric!.Value,
            request.Limit,
            request.CategoryId,
            cancellationToken));
    }

    [HttpGet("categories")]
    [ProducesResponseType<CategoryPerformanceResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<CategoryPerformanceResponse>> GetCategories(
        [FromQuery] AnalyticsDateRangeRequest request,
        CancellationToken cancellationToken)
    {
        return Ok(await analyticsService.GetCategoryPerformanceAsync(
            request.From!.Value,
            request.To!.Value,
            cancellationToken));
    }

    [HttpGet("returns")]
    [ProducesResponseType<ReturnAnalyticsResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ReturnAnalyticsResponse>> GetReturns(
        [FromQuery] AnalyticsDateRangeRequest request,
        CancellationToken cancellationToken)
    {
        return Ok(await analyticsService.GetReturnAnalyticsAsync(
            request.From!.Value,
            request.To!.Value,
            cancellationToken));
    }
}
