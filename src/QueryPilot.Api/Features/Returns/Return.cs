using QueryPilot.Api.Features.Orders;

namespace QueryPilot.Api.Features.Returns;

public sealed class Return
{
    public const int ReasonMaxLength = 500;

    public long Id { get; set; }

    public long OrderItemId { get; set; }

    public int Quantity { get; set; }

    public string Reason { get; set; } = string.Empty;

    public DateTime ReturnDate { get; set; }

    /// <summary>
    /// Stores the refund amount calculated from the OrderItem price snapshot.
    /// </summary>
    public decimal Amount { get; set; }

    public OrderItem OrderItem { get; set; } = null!;
}
