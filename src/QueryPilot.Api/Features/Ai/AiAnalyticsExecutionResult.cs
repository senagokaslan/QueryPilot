namespace QueryPilot.Api.Features.Ai;

public sealed record AiAnalyticsExecutionResult(
    AiAnalyticsExecutionStatus Status,
    string Message,
    ValidatedAnalyticsIntent? Intent,
    object? Data,
    AiExplanationResponse? Explanation,
    AiClarificationResponse? Clarification)
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
            explanation,
            Clarification: null);

    public static AiAnalyticsExecutionResult NeedsClarification(
        string originalQuestion,
        AiQuestionUnderstanding currentIntent,
        IReadOnlyList<AiClarificationField> requiredFields) =>
        new(
            AiAnalyticsExecutionStatus.NeedsClarification,
            "Analizi çalıştırabilmem için eksik veya belirsiz bilgileri tamamlayın.",
            Intent: null,
            Data: null,
            Explanation: null,
            new AiClarificationResponse(
                originalQuestion,
                currentIntent,
                requiredFields,
                "Eksik alanları soruya ekleyip aynı isteği yeniden gönderin."));
}
