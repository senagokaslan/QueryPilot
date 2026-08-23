using System.Text.Json.Serialization;

namespace QueryPilot.Api.Features.Analytics;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ComparisonStatus
{
    Increase = 1,
    Decrease = 2,
    NoChange = 3,
    NoBaseline = 4
}
