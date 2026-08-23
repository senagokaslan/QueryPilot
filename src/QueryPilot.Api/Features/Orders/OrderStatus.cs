using System.Text.Json.Serialization;

namespace QueryPilot.Api.Features.Orders;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum OrderStatus
{
    Pending = 1,
    Completed = 2,
    Cancelled = 3
}
