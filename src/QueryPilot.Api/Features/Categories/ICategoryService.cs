using QueryPilot.Api.Common.Pagination;
using QueryPilot.Api.Features.Categories.Dtos;

namespace QueryPilot.Api.Features.Categories;

public interface ICategoryService
{
    Task<PagedResponse<CategoryResponse>> GetAllAsync(
        CategoryListRequest request,
        CancellationToken cancellationToken = default);

    Task<CategoryResponse> GetByIdAsync(
        long id,
        CancellationToken cancellationToken = default);

    Task<CategoryResponse> CreateAsync(
        CreateCategoryRequest request,
        CancellationToken cancellationToken = default);

    Task<CategoryResponse> UpdateAsync(
        long id,
        UpdateCategoryRequest request,
        CancellationToken cancellationToken = default);

    Task DeactivateAsync(
        long id,
        CancellationToken cancellationToken = default);
}
