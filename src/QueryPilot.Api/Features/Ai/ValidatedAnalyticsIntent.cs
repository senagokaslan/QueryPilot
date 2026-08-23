using QueryPilot.Api.Features.Analytics;

namespace QueryPilot.Api.Features.Ai;

public sealed record ValidatedAnalyticsIntent(
    AiAnalysisType Analysis,
    DateTimeOffset FromUtc,
    DateTimeOffset ToUtc,
    string? ProductName,
    long? CategoryId,
    string? CategoryName,
    AnalyticsGranularity? Granularity,
    TopProductsMetric? Metric,
    int? Limit);
