using QueryPilot.Api.Common.Pagination;
using QueryPilot.Api.Features.Products.Dtos;

namespace QueryPilot.Api.Features.Products;

public interface IProductService
{
    Task<PagedResponse<ProductResponse>> GetAllAsync(
        ProductListRequest request,
        CancellationToken cancellationToken = default);

    Task<ProductResponse> GetByIdAsync(
        long id,
        CancellationToken cancellationToken = default);

    Task<ProductResponse> CreateAsync(
        CreateProductRequest request,
        CancellationToken cancellationToken = default);

    Task<ProductResponse> UpdateAsync(
        long id,
        UpdateProductRequest request,
        CancellationToken cancellationToken = default);

    Task DeactivateAsync(
        long id,
        CancellationToken cancellationToken = default);
}
