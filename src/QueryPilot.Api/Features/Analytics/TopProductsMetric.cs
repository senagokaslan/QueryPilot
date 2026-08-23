using System.Text.Json.Serialization;

namespace QueryPilot.Api.Features.Analytics;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum TopProductsMetric
{
    Quantity = 1,
    Revenue = 2
}
