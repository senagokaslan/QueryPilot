using System.ComponentModel.DataAnnotations;

namespace QueryPilot.Api.Features.Analytics.Dtos;

public class AnalyticsDateRangeRequest
{
    [Required]
    public DateTimeOffset? From { get; init; }

    [Required]
    public DateTimeOffset? To { get; init; }
}
