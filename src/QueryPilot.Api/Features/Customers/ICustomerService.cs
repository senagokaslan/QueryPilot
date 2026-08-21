using QueryPilot.Api.Common.Pagination;
using QueryPilot.Api.Features.Customers.Dtos;

namespace QueryPilot.Api.Features.Customers;

public interface ICustomerService
{
    Task<PagedResponse<CustomerResponse>> GetAllAsync(
        CustomerListRequest request,
        CancellationToken cancellationToken = default);

    Task<CustomerResponse> GetByIdAsync(
        long id,
        CancellationToken cancellationToken = default);

    Task<CustomerResponse> CreateAsync(
        CreateCustomerRequest request,
        CancellationToken cancellationToken = default);

    Task<CustomerResponse> UpdateAsync(
        long id,
        UpdateCustomerRequest request,
        CancellationToken cancellationToken = default);

    Task DeactivateAsync(
        long id,
        CancellationToken cancellationToken = default);
}
