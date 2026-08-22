using QueryPilot.Api.Common.Pagination;
using QueryPilot.Api.Features.Orders.Dtos;

namespace QueryPilot.Api.Features.Orders;

public interface IOrderService
{
    Task<PagedResponse<OrderListResponse>> GetAllAsync(
        OrderListRequest request,
        CancellationToken cancellationToken = default);

    Task<OrderDetailResponse> GetByIdAsync(
        long id,
        CancellationToken cancellationToken = default);

    Task<OrderDetailResponse> CreateAsync(
        CreateOrderRequest request,
        CancellationToken cancellationToken = default);
}
