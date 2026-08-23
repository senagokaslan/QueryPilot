using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using QueryPilot.Api.Configuration;
using QueryPilot.Api.Features.Ai;
using Xunit;

namespace QueryPilot.Api.Tests.Features.Ai;

public sealed class OpenAiSmokeTests
{
    [OpenAiSmokeFact]
    [Trait("Category", "OpenAiSmoke")]
    public async Task Live_provider_can_extract_a_supported_intent_when_explicitly_enabled()
    {
        var apiKey = Environment.GetEnvironmentVariable("AI__ApiKey")!;
        var model = Environment.GetEnvironmentVariable("AI__Model")!;
        using var client = new HttpClient
        {
            BaseAddress = new Uri("https://api.openai.com/v1/"),
            Timeout = Timeout.InfiniteTimeSpan
        };
        var service = new OpenAiService(
            client,
            Options.Create(new AiOptions
            {
                Provider = "OpenAI",
                Model = model,
                ApiKey = apiKey,
                TimeoutSeconds = 30
            }),
            NullLogger<OpenAiService>.Instance);

        var result = await service.UnderstandQuestionAsync(
            "Bu ay satışlarımız nasıl?");

        Assert.Equal(AiAnalysisType.SalesSummary, result.Analysis);
        Assert.Equal("bu ay", result.Period, ignoreCase: true);
    }
}

public sealed class OpenAiSmokeFactAttribute : FactAttribute
{
    public OpenAiSmokeFactAttribute()
    {
        var explicitlyEnabled = string.Equals(
            Environment.GetEnvironmentVariable("QUERYPILOT_RUN_OPENAI_SMOKE"),
            "true",
            StringComparison.OrdinalIgnoreCase);
        var hasCredential = !string.IsNullOrWhiteSpace(
            Environment.GetEnvironmentVariable("AI__ApiKey"));
        var hasModel = !string.IsNullOrWhiteSpace(
            Environment.GetEnvironmentVariable("AI__Model"));

        if (!explicitlyEnabled || !hasCredential || !hasModel)
        {
            Skip = "Set QUERYPILOT_RUN_OPENAI_SMOKE=true, AI__ApiKey and AI__Model "
                + "to run the paid live-provider smoke test.";
        }
    }
}
