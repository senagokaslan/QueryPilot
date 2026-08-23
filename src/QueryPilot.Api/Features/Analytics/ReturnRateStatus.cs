using System.Text.Json.Serialization;

namespace QueryPilot.Api.Features.Analytics;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ReturnRateStatus
{
    Calculated = 1,
    NoSalesBaseline = 2
}
