using System.ComponentModel.DataAnnotations;

namespace QueryPilot.Api.Features.Analytics.Dtos;

public sealed class SalesTrendRequest : AnalyticsDateRangeRequest
{
    [Required]
    [EnumDataType(typeof(AnalyticsGranularity))]
    public AnalyticsGranularity? Granularity { get; init; }
}
