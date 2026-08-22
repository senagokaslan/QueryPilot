using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace QueryPilot.Api.Features.Orders.Dtos;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class CreateOrderItemRequest
{
    [Range(1, long.MaxValue)]
    public long ProductId { get; init; }

    [Range(1, int.MaxValue)]
    public int Quantity { get; init; }
}
