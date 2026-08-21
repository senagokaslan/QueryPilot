using Microsoft.AspNetCore.Mvc;
using QueryPilot.Api.Common.ErrorHandling;
using QueryPilot.Api.Common.Pagination;
using QueryPilot.Api.Features.Categories.Dtos;

namespace QueryPilot.Api.Features.Categories;

[ApiController]
[Route("api/categories")]
public sealed class CategoriesController(ICategoryService categoryService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<PagedResponse<CategoryResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedResponse<CategoryResponse>>> GetAll(
        [FromQuery] CategoryListRequest request,
        CancellationToken cancellationToken)
    {
        return Ok(await categoryService.GetAllAsync(request, cancellationToken));
    }

    [HttpGet("{id:long}")]
    [ProducesResponseType<CategoryResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CategoryResponse>> GetById(
        long id,
        CancellationToken cancellationToken)
    {
        return Ok(await categoryService.GetByIdAsync(id, cancellationToken));
    }

    [HttpPost]
    [ProducesResponseType<CategoryResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CategoryResponse>> Create(
        CreateCategoryRequest request,
        CancellationToken cancellationToken)
    {
        var category = await categoryService.CreateAsync(request, cancellationToken);

        return CreatedAtAction(nameof(GetById), new { id = category.Id }, category);
    }

    [HttpPut("{id:long}")]
    [ProducesResponseType<CategoryResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CategoryResponse>> Update(
        long id,
        UpdateCategoryRequest request,
        CancellationToken cancellationToken)
    {
        return Ok(await categoryService.UpdateAsync(id, request, cancellationToken));
    }

    [HttpDelete("{id:long}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Deactivate(
        long id,
        CancellationToken cancellationToken)
    {
        await categoryService.DeactivateAsync(id, cancellationToken);
        return NoContent();
    }
}
