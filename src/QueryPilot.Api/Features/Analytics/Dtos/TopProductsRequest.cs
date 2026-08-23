using System.ComponentModel.DataAnnotations;

namespace QueryPilot.Api.Features.Analytics.Dtos;

public sealed class TopProductsRequest : AnalyticsDateRangeRequest
{
    [Required]
    [EnumDataType(typeof(TopProductsMetric))]
    public TopProductsMetric? Metric { get; init; }

    [Range(1, AnalyticsService.MaximumTopProductsLimit)]
    public int? Limit { get; init; }

    [Range(1, long.MaxValue)]
    public long? CategoryId { get; init; }
}
