using QueryPilot.Api.Features.Customers;

namespace QueryPilot.Api.Features.Orders;

public sealed class Order
{
    public long Id { get; set; }

    public long CustomerId { get; set; }

    public DateTime OrderDate { get; set; }

    public OrderStatus Status { get; set; }

    public decimal TotalAmount { get; set; }

    public DateTime CreatedAt { get; set; }

    public Customer Customer { get; set; } = null!;

    public ICollection<OrderItem> Items { get; } = [];
}
