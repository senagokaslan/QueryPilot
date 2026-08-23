namespace QueryPilot.Api.Features.Ai;

public sealed record AiQuestionUnderstanding(
    AiAnalysisType Analysis,
    string? Period,
    string? From,
    string? To,
    string? ProductName,
    string? CategoryName,
    AiTopProductsMetric? Metric,
    int? Limit);
