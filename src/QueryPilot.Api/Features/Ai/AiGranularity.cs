using System.Text.Json.Serialization;

namespace QueryPilot.Api.Features.Ai;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum AiGranularity
{
    Daily = 1,
    Weekly = 2,
    Monthly = 3
}
