using QueryPilot.Api.Features.Analytics.Dtos;

namespace QueryPilot.Api.Features.Ai;

/// <summary>
/// Unified AI query result. Data always contains the structured backend result for a
/// completed query; Explanation is optional and never replaces Data.
/// </summary>
public sealed record AiAnalyticsExecutionResult(
    AiAnalyticsExecutionStatus Status,
    string Message,
    ValidatedAnalyticsIntent? Intent,
    object? Data,
    AiChartDataResponse? ChartData,
    string? Warning,
    AiExplanationResponse? Explanation,
    AiClarificationResponse? Clarification,
    AiUnsupportedResponse? Unsupported)
{
    public static AiAnalyticsExecutionResult Completed(
        ValidatedAnalyticsIntent intent,
        object data,
        AiExplanationResponse explanation) =>
        new(
            AiAnalyticsExecutionStatus.Completed,
            "Analytics result is ready.",
            intent,
            data,
            CreateChartData(data),
            CreateExplanationWarning(explanation.Status),
            explanation,
            Clarification: null,
            Unsupported: null);

    public static AiAnalyticsExecutionResult NeedsClarification(
        string originalQuestion,
        AiQuestionUnderstanding currentIntent,
        IReadOnlyList<AiClarificationField> requiredFields) =>
        new(
            AiAnalyticsExecutionStatus.NeedsClarification,
            "Analizi çalıştırabilmem için eksik veya belirsiz bilgileri tamamlayın.",
            Intent: null,
            Data: null,
            ChartData: null,
            Warning: null,
            Explanation: null,
            new AiClarificationResponse(
                originalQuestion,
                currentIntent,
                requiredFields,
                "Eksik alanları soruya ekleyip aynı isteği yeniden gönderin."),
            Unsupported: null);

    public static AiAnalyticsExecutionResult UnsupportedQuestion() =>
        new(
            AiAnalyticsExecutionStatus.Unsupported,
            "Bu soru QueryPilot'ın desteklediği BI analizlerinin dışında.",
            Intent: null,
            Data: null,
            ChartData: null,
            Warning: null,
            Explanation: null,
            Clarification: null,
            AiUnsupportedResponse.Create());

    private static AiChartDataResponse? CreateChartData(object data) =>
        data is SalesTrendResponse trend
            ? new AiChartDataResponse(
                trend.Revenue,
                trend.OrderCount,
                trend.UnitsSold)
            : null;

    private static string? CreateExplanationWarning(AiExplanationStatus status) =>
        status switch
        {
            AiExplanationStatus.Unavailable =>
                "AI açıklaması şu anda hazırlanamadı; numeric analytics sonucu geçerlidir.",
            AiExplanationStatus.RejectedUnsafe =>
                "AI açıklaması güvenlik kontrolünden geçmedi; numeric analytics sonucu geçerlidir.",
            _ => null
        };
}
