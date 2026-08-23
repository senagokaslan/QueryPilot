using System.Text.Json.Serialization;

namespace QueryPilot.Api.Features.Ai;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum AiTopProductsMetric
{
    Quantity = 1,
    Revenue = 2
}
