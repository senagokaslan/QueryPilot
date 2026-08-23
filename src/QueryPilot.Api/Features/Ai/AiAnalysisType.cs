using System.Text.Json.Serialization;

namespace QueryPilot.Api.Features.Ai;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum AiAnalysisType
{
    Unknown = 0,
    SalesSummary = 1,
    SalesTrend = 2,
    TopProducts = 3,
    CategoryPerformance = 4,
    ReturnAnalysis = 5
}
