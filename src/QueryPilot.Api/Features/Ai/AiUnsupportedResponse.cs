namespace QueryPilot.Api.Features.Ai;

public sealed record AiUnsupportedResponse(
    IReadOnlyList<string> SupportedAnalyses,
    IReadOnlyList<string> ExampleQuestions)
{
    private static readonly IReadOnlyList<string> Analyses =
    [
        "Satış özeti",
        "Satış trendi",
        "En çok satan ürünler",
        "Kategori performansı",
        "İade analizi"
    ];

    private static readonly IReadOnlyList<string> Examples =
    [
        "Son ayın satış özeti nedir?",
        "Son 3 ayın aylık satış trendini göster.",
        "Bu yıl gelire göre en çok satan 5 ürün hangileri?",
        "Geçen ay kategorilerin satış performansı nasıldı?",
        "Son 30 gündeki iadeleri analiz et."
    ];

    public static AiUnsupportedResponse Create() => new(Analyses, Examples);
}
