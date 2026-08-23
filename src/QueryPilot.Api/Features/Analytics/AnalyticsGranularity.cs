using System.Text.Json.Serialization;

namespace QueryPilot.Api.Features.Analytics;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum AnalyticsGranularity
{
    Daily = 1,
    Weekly = 2,
    Monthly = 3
}
