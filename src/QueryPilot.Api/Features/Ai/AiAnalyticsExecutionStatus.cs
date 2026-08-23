using System.Text.Json.Serialization;

namespace QueryPilot.Api.Features.Ai;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum AiAnalyticsExecutionStatus
{
    Completed = 1,
    NeedsClarification = 2,
    Unsupported = 3
}
