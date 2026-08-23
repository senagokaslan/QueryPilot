namespace QueryPilot.Api.Features.Analytics;

using QueryPilot.Api.Features.Analytics.Dtos;

public interface IAnalyticsService
{
    AnalyticsDateRange CreateDateRange(
        DateTimeOffset from,
        DateTimeOffset to);

    Task<SalesSummaryResponse> GetSalesSummaryAsync(
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken = default);

    Task<SalesTrendResponse> GetSalesTrendAsync(
        DateTimeOffset from,
        DateTimeOffset to,
        AnalyticsGranularity granularity,
        CancellationToken cancellationToken = default);

    Task<TopProductsResponse> GetTopProductsAsync(
        DateTimeOffset from,
        DateTimeOffset to,
        TopProductsMetric metric,
        int? limit = null,
        long? categoryId = null,
        CancellationToken cancellationToken = default);

    Task<CategoryPerformanceResponse> GetCategoryPerformanceAsync(
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken = default);

    Task<ReturnAnalyticsResponse> GetReturnAnalyticsAsync(
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken = default);
}
