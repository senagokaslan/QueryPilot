using QueryPilot.Api.Features.Products;

namespace QueryPilot.Api.Features.Orders;

public sealed class OrderItem
{
    public long Id { get; set; }

    public long OrderId { get; set; }

    public long ProductId { get; set; }

    public int Quantity { get; set; }

    /// <summary>
    /// Stores the product price at the time of sale. Later product price changes
    /// must not modify this snapshot.
    /// </summary>
    public decimal UnitPrice { get; set; }

    public decimal LineTotal { get; set; }

    public Order Order { get; set; } = null!;

    public Product Product { get; set; } = null!;
}
