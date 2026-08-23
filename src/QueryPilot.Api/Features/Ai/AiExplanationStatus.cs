using System.Text.Json.Serialization;

namespace QueryPilot.Api.Features.Ai;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum AiExplanationStatus
{
    Available = 1,
    Unavailable = 2,
    RejectedUnsafe = 3
}
