using QueryPilot.Api.Features.Orders.Dtos;

namespace QueryPilot.Api.Features.Orders;

public interface IOrderService
{
    Task<OrderDetailResponse> GetByIdAsync(
        long id,
        CancellationToken cancellationToken = default);

    Task<OrderDetailResponse> CreateAsync(
        CreateOrderRequest request,
        CancellationToken cancellationToken = default);
}
