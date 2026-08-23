
namespace QueryPilot.Api.Features.Analytics;

/// <summary>
/// Represents a normalized UTC range whose start is inclusive and end is exclusive.
/// </summary>
public sealed record AnalyticsDateRange
{
    internal AnalyticsDateRange(
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc)
    {
        FromUtc = fromUtc;
        ToUtc = toUtc;
    }

    public DateTimeOffset FromUtc { get; }

    public DateTimeOffset ToUtc { get; }

    public TimeSpan Duration => ToUtc - FromUtc;

    public bool Contains(DateTimeOffset value)
    {
        var valueUtc = value.ToUniversalTime();
        return valueUtc >= FromUtc && valueUtc < ToUtc;
    }
}
