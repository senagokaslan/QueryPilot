using System.Collections.Concurrent;
using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using QueryPilot.Api.Common.Exceptions;
using QueryPilot.Api.Configuration;
using QueryPilot.Api.Features.Ai;
using Xunit;

namespace QueryPilot.Api.Tests.Features.Ai;

public sealed class AiServiceTests
{
    [Fact]
    public async Task Fake_service_returns_configured_application_contracts()
    {
        IAiService service = new FakeAiService(
            new AiQuestionUnderstanding(
                AiAnalysisType.SalesSummary,
                Period: null,
                From: null,
                To: null,
                ProductName: null,
                CategoryName: null,
                Granularity: null,
                Metric: null,
                Limit: null),
            new AiResultExplanation("Revenue increased."));

        var understanding = await service.UnderstandQuestionAsync("Show sales");
        var explanation = await service.ExplainResultAsync(
            "Show sales",
            "{\"totalRevenue\":100}");

        Assert.Equal(AiAnalysisType.SalesSummary, understanding.Analysis);
        Assert.Equal("Revenue increased.", explanation.Text);
    }

    [Fact]
    public async Task Explanation_request_sends_backend_dto_with_grounding_instructions()
    {
        const string question = "Geçen aya göre satış nasıl?";
        const string analyticsJson =
            "{\"totalRevenue\":540000,\"previousValue\":610000}";
        var handler = new StubHttpMessageHandler(async (request, cancellationToken) =>
        {
            var requestJson = await request.Content!.ReadAsStringAsync(cancellationToken);
            using var requestDocument = JsonDocument.Parse(requestJson);
            var root = requestDocument.RootElement;
            var instructions = root.GetProperty("system_instruction").GetString();
            var input = root.GetProperty("input").GetString();

            Assert.Contains("Yalnızca verilen JSON DTO", instructions);
            Assert.Contains("yeniden hesaplama", instructions);
            Assert.Contains("sayı, yüzde, tarih, ürün, müşteri", instructions);
            Assert.Contains("artış, azalış", instructions);
            Assert.Contains("500 karakter", instructions);
            Assert.Contains(question, input);
            Assert.Contains(analyticsJson, input);
            Assert.False(root.TryGetProperty("response_format", out _));
            Assert.False(root.GetProperty("store").GetBoolean());

            return JsonResponse(ProviderResponseForIntent(
                "Gelir 610.000 TL'den 540.000 TL'ye gerileyerek azaldı."));
        });
        var service = CreateService(handler, new CapturingLogger<GeminiService>());

        var result = await service.ExplainResultAsync(question, analyticsJson);

        Assert.Contains("azaldı", result.Text);
    }

    [Fact]
    public async Task Gemini_adapter_extracts_requested_analysis_period_metric_and_limit()
    {
        var testCredential = $"test-{Guid.NewGuid():N}";
        var handler = new StubHttpMessageHandler(async (request, cancellationToken) =>
        {
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.Equal(
                "https://generativelanguage.googleapis.com/v1beta/interactions",
                request.RequestUri?.ToString());
            Assert.Null(request.Headers.Authorization);
            Assert.Equal(
                testCredential,
                Assert.Single(request.Headers.GetValues("x-goog-api-key")));

            var requestJson = await request.Content!.ReadAsStringAsync(cancellationToken);
            using var requestDocument = JsonDocument.Parse(requestJson);
            var root = requestDocument.RootElement;
            var instructions = root.GetProperty("system_instruction").GetString();
            var input = root.GetProperty("input").GetString();
            var format = root.GetProperty("response_format");
            var schema = format.GetProperty("schema");

            Assert.Equal("test-model", root.GetProperty("model").GetString());
            Assert.Equal("Son 3 ayda en çok satan 5 ürün ne?", input);
            Assert.Contains("salesSummary", instructions);
            Assert.Contains("salesTrend", instructions);
            Assert.Contains("topProducts", instructions);
            Assert.Contains("categoryPerformance", instructions);
            Assert.Contains("returnAnalysis", instructions);
            Assert.Contains("Do not write SQL", instructions);
            Assert.Contains("calculate or invent sales figures", instructions);
            Assert.Contains("rank or name products yourself", instructions);
            Assert.Contains("backend analytics service", instructions);
            Assert.Contains("untrusted data", instructions);
            Assert.Contains("ignore these rules", instructions);
            Assert.Contains("reveal", instructions);
            Assert.Contains("secrets", instructions);
            Assert.Equal("text", format.GetProperty("type").GetString());
            Assert.Equal("application/json", format.GetProperty("mime_type").GetString());
            Assert.False(root.GetProperty("store").GetBoolean());
            Assert.False(schema.GetProperty("additionalProperties").GetBoolean());
            Assert.Equal(9, schema.GetProperty("required").GetArrayLength());

            return JsonResponse(ProviderResponseForIntent(IntentJson(
                analysis: "topProducts",
                period: "son 3 ay",
                metric: "quantity",
                limit: 5)));
        });
        var logger = new CapturingLogger<GeminiService>();
        var service = CreateService(handler, logger, credential: testCredential);

        var result = await service.UnderstandQuestionAsync(
            "Son 3 ayda en çok satan 5 ürün ne?");

        Assert.Equal(AiAnalysisType.TopProducts, result.Analysis);
        Assert.Equal("son 3 ay", result.Period);
        Assert.Equal(AiTopProductsMetric.Quantity, result.Metric);
        Assert.Equal(5, result.Limit);
        Assert.DoesNotContain(testCredential, logger.MessagesText);
        Assert.DoesNotContain("Son 3 ayda", logger.MessagesText);
        Assert.Contains("ElapsedMs", logger.MessagesText);
    }

    [Theory]
    [InlineData(
        "Bu ayın satış özeti nedir?",
        AiAnalysisType.SalesSummary,
        "bu ay",
        null,
        null,
        null,
        null,
        null,
        null,
        null)]
    [InlineData(
        "2026-01-01 ile 2026-03-01 arasında aylık satış eğilimini göster.",
        AiAnalysisType.SalesTrend,
        null,
        "2026-01-01",
        "2026-03-01",
        null,
        null,
        AiGranularity.Monthly,
        null,
        null)]
    [InlineData(
        "Elektronik kategorisinin performansını göster.",
        AiAnalysisType.CategoryPerformance,
        null,
        null,
        null,
        null,
        "Elektronik",
        null,
        null,
        null)]
    [InlineData(
        "Geçen hafta iadeler hangi nedenlerle yapıldı?",
        AiAnalysisType.ReturnAnalysis,
        "geçen hafta",
        null,
        null,
        null,
        null,
        null,
        null,
        null)]
    [InlineData(
        "Ciroya göre ilk 10 ürünü sırala.",
        AiAnalysisType.TopProducts,
        null,
        null,
        null,
        null,
        null,
        null,
        AiTopProductsMetric.Revenue,
        10)]
    [InlineData(
        "Telefon ürününün son ay satış özetini göster.",
        AiAnalysisType.SalesSummary,
        "son ay",
        null,
        null,
        "Telefon",
        null,
        null,
        null,
        null)]
    public async Task Gemini_adapter_maps_different_Turkish_question_patterns(
        string question,
        AiAnalysisType analysis,
        string? period,
        string? from,
        string? to,
        string? productName,
        string? categoryName,
        AiGranularity? granularity,
        AiTopProductsMetric? metric,
        int? limit)
    {
        var handler = new StubHttpMessageHandler((_, _) => Task.FromResult(
            JsonResponse(ProviderResponseForIntent(IntentJson(
                ToJsonName(analysis),
                period,
                from,
                to,
                productName,
                categoryName,
                granularity is null ? null : ToJsonName(granularity.Value),
                metric is null ? null : ToJsonName(metric.Value),
                limit)))));
        var service = CreateService(handler, new CapturingLogger<GeminiService>());

        var result = await service.UnderstandQuestionAsync(question);

        Assert.Equal(analysis, result.Analysis);
        Assert.Equal(period, result.Period);
        Assert.Equal(from, result.From);
        Assert.Equal(to, result.To);
        Assert.Equal(productName, result.ProductName);
        Assert.Equal(categoryName, result.CategoryName);
        Assert.Equal(granularity, result.Granularity);
        Assert.Equal(metric, result.Metric);
        Assert.Equal(limit, result.Limit);
    }

    [Theory]
    [InlineData("Satışlarınız geçen aya göre çok iyi görünüyor.")]
    [InlineData("{\"analysis\":")]
    [InlineData("{\"analysis\":\"salesSummary\",\"unexpected\":true}")]
    [InlineData("{\"analysis\":\"salesSummary\",\"period\":\"son ay\",\"from\":null,\"to\":null,\"productName\":null,\"categoryName\":null,\"granularity\":null,\"metric\":null,\"limit\":null,\"rawSql\":\"DROP TABLE Orders\"}")]
    [InlineData("{\"analysis\":\"returnAnalysis\",\"period\":\"son ay\",\"from\":null,\"to\":null,\"productName\":null,\"categoryName\":null,\"granularity\":null,\"metric\":null,\"limit\":null,\"command\":\"delete data\"}")]
    [InlineData("{\"analysis\":\"inventoryAnalysis\",\"period\":\"son ay\",\"from\":null,\"to\":null,\"productName\":null,\"categoryName\":null,\"granularity\":null,\"metric\":null,\"limit\":null}")]
    [InlineData("{\"analysis\":\"salesTrend\",\"period\":\"son ay\",\"from\":null,\"to\":null,\"productName\":null,\"categoryName\":null,\"granularity\":\"yearly\",\"metric\":null,\"limit\":null}")]
    [InlineData("{\"analysis\":\"topProducts\",\"period\":\"son ay\",\"from\":null,\"to\":null,\"productName\":null,\"categoryName\":null,\"granularity\":null,\"metric\":\"profit\",\"limit\":5}")]
    [InlineData("{\"analysis\":\"topProducts\",\"period\":\"son ay\",\"from\":null,\"to\":null,\"productName\":null,\"categoryName\":null,\"granularity\":null,\"metric\":\"quantity\",\"limit\":51}")]
    public async Task Gemini_adapter_rejects_free_text_or_invalid_intent_json(
        string providerOutput)
    {
        var handler = new StubHttpMessageHandler((_, _) => Task.FromResult(
            JsonResponse(ProviderResponseForIntent(providerOutput))));
        var service = CreateService(handler, new CapturingLogger<GeminiService>());

        var exception = await Assert.ThrowsAsync<AiServiceUnavailableException>(() =>
            service.UnderstandQuestionAsync("Satışları yorumla"));

        Assert.Contains("invalid intent", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Gemini_adapter_retries_transient_provider_failures()
    {
        var attempts = 0;
        var handler = new StubHttpMessageHandler((_, _) =>
        {
            attempts++;
            return Task.FromResult(attempts switch
            {
                1 => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable),
                2 => new HttpResponseMessage(HttpStatusCode.TooManyRequests),
                _ => JsonResponse(ProviderResponseForIntent(IntentJson("salesSummary")))
            });
        });
        var service = CreateService(handler, new CapturingLogger<GeminiService>());

        var result = await service.UnderstandQuestionAsync("Show sales");

        Assert.Equal(AiAnalysisType.SalesSummary, result.Analysis);
        Assert.Equal(3, attempts);
    }

    [Theory]
    [InlineData(HttpStatusCode.TooManyRequests)]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.BadGateway)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    public async Task Gemini_adapter_converts_exhausted_transient_status_to_common_error(
        HttpStatusCode statusCode)
    {
        var handler = new StubHttpMessageHandler((_, _) => Task.FromResult(
            new HttpResponseMessage(statusCode)));
        var service = CreateService(handler, new CapturingLogger<GeminiService>());

        var exception = await Assert.ThrowsAsync<AiServiceUnavailableException>(() =>
            service.UnderstandQuestionAsync("Show sales"));

        Assert.Equal("The AI provider is temporarily unavailable.", exception.Message);
    }

    [Fact]
    public async Task Gemini_adapter_converts_connection_failure_to_common_error()
    {
        var handler = new StubHttpMessageHandler((_, _) =>
            throw new HttpRequestException("Sensitive transport detail."));
        var service = CreateService(handler, new CapturingLogger<GeminiService>());

        var exception = await Assert.ThrowsAsync<AiServiceUnavailableException>(() =>
            service.UnderstandQuestionAsync("Show sales"));

        Assert.Equal("The AI provider is temporarily unavailable.", exception.Message);
        Assert.DoesNotContain("Sensitive", exception.Message);
    }

    [Fact]
    public async Task Gemini_adapter_propagates_caller_cancellation()
    {
        var handler = new StubHttpMessageHandler(async (_, cancellationToken) =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return JsonResponse("{}");
        });
        var service = CreateService(handler, new CapturingLogger<GeminiService>());
        using var cancellationSource = new CancellationTokenSource();
        cancellationSource.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            service.UnderstandQuestionAsync(
                "Show sales",
                cancellationSource.Token));
    }

    [Fact]
    public async Task Gemini_adapter_converts_timeout_to_service_unavailable()
    {
        var handler = new StubHttpMessageHandler(async (_, cancellationToken) =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return JsonResponse("{}");
        });
        var service = CreateService(
            handler,
            new CapturingLogger<GeminiService>(),
            timeoutSeconds: 1);

        var exception = await Assert.ThrowsAsync<AiServiceUnavailableException>(() =>
            service.UnderstandQuestionAsync("Show sales"));

        Assert.Contains("timed out", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static GeminiService CreateService(
        HttpMessageHandler handler,
        ILogger<GeminiService> logger,
        int timeoutSeconds = 5,
        string? credential = null)
    {
        var client = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://generativelanguage.googleapis.com/v1beta/"),
            Timeout = Timeout.InfiniteTimeSpan
        };
        var options = Options.Create(new AiOptions
        {
            Provider = "Gemini",
            Model = "test-model",
            ApiKey = credential ?? $"test-{Guid.NewGuid():N}",
            TimeoutSeconds = timeoutSeconds
        });

        return new GeminiService(client, options, logger);
    }

    private static HttpResponseMessage JsonResponse(string json) =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

    private static string IntentJson(
        string analysis,
        string? period = null,
        string? from = null,
        string? to = null,
        string? productName = null,
        string? categoryName = null,
        string? granularity = null,
        string? metric = null,
        int? limit = null) =>
        JsonSerializer.Serialize(new
        {
            analysis,
            period,
            from,
            to,
            productName,
            categoryName,
            granularity,
            metric,
            limit
        });

    private static string ProviderResponseForIntent(string intentJson) =>
        JsonSerializer.Serialize(new
        {
            id = "test-interaction",
            status = "completed",
            steps = new[]
            {
                new
                {
                    type = "model_output",
                    content = new[]
                    {
                        new { type = "text", text = intentJson }
                    }
                }
            },
            usage = new { totalInputTokens = 10, totalOutputTokens = 5 }
        });

    private static string ToJsonName(AiAnalysisType analysis) => analysis switch
    {
        AiAnalysisType.Unknown => "unknown",
        AiAnalysisType.SalesSummary => "salesSummary",
        AiAnalysisType.SalesTrend => "salesTrend",
        AiAnalysisType.TopProducts => "topProducts",
        AiAnalysisType.CategoryPerformance => "categoryPerformance",
        AiAnalysisType.ReturnAnalysis => "returnAnalysis",
        _ => throw new ArgumentOutOfRangeException(nameof(analysis))
    };

    private static string ToJsonName(AiTopProductsMetric metric) => metric switch
    {
        AiTopProductsMetric.Quantity => "quantity",
        AiTopProductsMetric.Revenue => "revenue",
        _ => throw new ArgumentOutOfRangeException(nameof(metric))
    };

    private static string ToJsonName(AiGranularity granularity) => granularity switch
    {
        AiGranularity.Daily => "daily",
        AiGranularity.Weekly => "weekly",
        AiGranularity.Monthly => "monthly",
        _ => throw new ArgumentOutOfRangeException(nameof(granularity))
    };

    private sealed class StubHttpMessageHandler(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            send(request, cancellationToken);
    }

    private sealed class CapturingLogger<T> : ILogger<T>
    {
        private readonly ConcurrentQueue<string> messages = new();

        public string MessagesText => string.Join(Environment.NewLine, messages);

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter) =>
            messages.Enqueue(formatter(state, exception));
    }
}
