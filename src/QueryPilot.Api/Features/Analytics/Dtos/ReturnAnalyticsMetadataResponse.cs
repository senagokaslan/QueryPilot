namespace QueryPilot.Api.Features.Analytics.Dtos;

public sealed record ReturnAnalyticsMetadataResponse(
    string ReturnRateDefinition,
    string ReturnDateFilter,
    string SalesDateFilter,
    int MostReturnedProductsLimit);
