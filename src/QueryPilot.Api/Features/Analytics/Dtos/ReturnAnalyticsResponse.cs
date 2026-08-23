namespace QueryPilot.Api.Features.Analytics.Dtos;

public sealed record ReturnAnalyticsResponse(
    DateTimeOffset FromUtc,
    DateTimeOffset ToUtc,
    int ReturnRecordCount,
    long ReturnedQuantity,
    decimal TotalReturnAmount,
    long SoldQuantity,
    decimal? ReturnRatePercentage,
    ReturnRateStatus ReturnRateStatus,
    IReadOnlyList<ReturnReasonBreakdownResponse> Reasons,
    IReadOnlyList<ReturnedProductResponse> MostReturnedProducts,
    ReturnAnalyticsMetadataResponse Metadata);
