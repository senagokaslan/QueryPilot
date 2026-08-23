namespace QueryPilot.Api.Features.Ai;

public sealed record AiAnalyticsExecutionResult(
    AiAnalyticsExecutionStatus Status,
    string Message,
    ValidatedAnalyticsIntent? Intent,
    object? Data,
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
            Warning: null,
            Explanation: null,
            Clarification: null,
            AiUnsupportedResponse.Create());

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
