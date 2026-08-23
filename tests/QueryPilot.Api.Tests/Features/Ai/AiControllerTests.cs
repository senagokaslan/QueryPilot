using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using QueryPilot.Api.Common.Exceptions;
using QueryPilot.Api.Features.Ai;
using QueryPilot.Api.Features.Analytics;
using QueryPilot.Api.Features.Analytics.Dtos;
using Xunit;

namespace QueryPilot.Api.Tests.Features.Ai;

public sealed class AiControllerTests
{
    private static readonly DateTimeOffset FromUtc =
        new(2026, 5, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset ToUtc =
        new(2026, 8, 1, 0, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Empty_question_fails_request_model_validation(string? question)
    {
        var request = new AiQueryRequest { Question = question! };

        var errors = Validate(request);

        Assert.NotEmpty(errors);
        Assert.Contains(errors, error => error.MemberNames.Contains("Question"));
    }

    [Fact]
    public void Question_over_maximum_length_fails_request_model_validation()
    {
        var request = new AiQueryRequest
        {
            Question = new string('a', AiQueryRequest.QuestionMaximumLength + 1)
        };

        var errors = Validate(request);

        Assert.NotEmpty(errors);
        Assert.Contains(errors, error => error.MemberNames.Contains("Question"));
    }

    [Fact]
    public async Task Valid_question_returns_analysis_parameters_data_chart_and_explanation()
    {
        var trend = new SalesTrendResponse(
            FromUtc,
            ToUtc,
            AnalyticsGranularity.Monthly,
            Revenue:
            [
                new AnalyticsChartPoint(FromUtc, "May 2026", 125000m)
            ],
            OrderCount: [],
            UnitsSold: []);
        var intent = new ValidatedAnalyticsIntent(
            AiAnalysisType.SalesTrend,
            FromUtc,
            ToUtc,
            ProductId: null,
            ProductName: null,
            CategoryId: null,
            CategoryName: null,
            AnalyticsGranularity.Monthly,
            Metric: null,
            Limit: null);
        var expected = AiAnalyticsExecutionResult.Completed(
            intent,
            trend,
            AiExplanationResponse.Available("Satış trendi yükseldi."));
        var coordinator = new StubCoordinator((_, _) => Task.FromResult(expected));
        var controller = new AiController(coordinator);

        var response = await controller.Query(
            new AiQueryRequest { Question = "  Son 3 ayın satış trendi nedir?  " },
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(response.Result);
        var result = Assert.IsType<AiAnalyticsExecutionResult>(ok.Value);
        Assert.Equal("Son 3 ayın satış trendi nedir?", coordinator.LastQuestion);
        Assert.Equal(AiAnalysisType.SalesTrend, result.Intent!.Analysis);
        Assert.Equal(AnalyticsGranularity.Monthly, result.Intent.Granularity);
        Assert.Same(trend, result.Data);
        Assert.Same(trend.Revenue, result.ChartData!.Revenue);
        Assert.Equal("Satış trendi yükseldi.", result.Explanation!.Text);
    }

    [Fact]
    public async Task Ambiguous_question_returns_clarification_contract()
    {
        var understanding = CreateUnderstanding(AiAnalysisType.TopProducts);
        var expected = AiAnalyticsExecutionResult.NeedsClarification(
            "En iyi ürünler hangileri?",
            understanding,
            [
                new AiClarificationField(
                    nameof(AiQuestionUnderstanding.Metric),
                    "Ürünleri satış adedine göre mi, gelire göre mi sıralayalım?",
                    ["quantity", "revenue"])
            ]);
        var controller = new AiController(
            new StubCoordinator((_, _) => Task.FromResult(expected)));

        var response = await controller.Query(
            new AiQueryRequest { Question = "En iyi ürünler hangileri?" },
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(response.Result);
        var result = Assert.IsType<AiAnalyticsExecutionResult>(ok.Value);
        Assert.Equal(AiAnalyticsExecutionStatus.NeedsClarification, result.Status);
        Assert.Null(result.Data);
        Assert.Single(result.Clarification!.RequiredFields);
    }

    [Fact]
    public async Task Unsupported_question_returns_supported_examples()
    {
        var expected = AiAnalyticsExecutionResult.UnsupportedQuestion();
        var controller = new AiController(
            new StubCoordinator((_, _) => Task.FromResult(expected)));

        var response = await controller.Query(
            new AiQueryRequest { Question = "Yarın hava nasıl?" },
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(response.Result);
        var result = Assert.IsType<AiAnalyticsExecutionResult>(ok.Value);
        Assert.Equal(AiAnalyticsExecutionStatus.Unsupported, result.Status);
        Assert.Null(result.Data);
        Assert.NotEmpty(result.Unsupported!.ExampleQuestions);
    }

    [Fact]
    public async Task Provider_failure_is_propagated_to_common_503_handler()
    {
        var coordinator = new StubCoordinator((_, _) =>
            Task.FromException<AiAnalyticsExecutionResult>(
                new AiServiceUnavailableException("Internal provider detail.")));
        var controller = new AiController(coordinator);

        await Assert.ThrowsAsync<AiServiceUnavailableException>(() =>
            controller.Query(
                new AiQueryRequest { Question = "Bu ayın satış özeti nedir?" },
                CancellationToken.None));
    }

    [Fact]
    public void Endpoint_declares_success_validation_and_provider_failure_for_swagger()
    {
        var method = typeof(AiController).GetMethod(nameof(AiController.Query))!;
        var httpPost = Assert.Single(
            method.GetCustomAttributes(typeof(HttpPostAttribute), inherit: true)
                .Cast<HttpPostAttribute>());
        var responseTypes = method
            .GetCustomAttributes(typeof(ProducesResponseTypeAttribute), inherit: true)
            .Cast<ProducesResponseTypeAttribute>()
            .Select(attribute => attribute.StatusCode)
            .ToArray();

        Assert.Equal("query", httpPost.Template);
        Assert.Contains(StatusCodes.Status200OK, responseTypes);
        Assert.Contains(StatusCodes.Status400BadRequest, responseTypes);
        Assert.Contains(StatusCodes.Status503ServiceUnavailable, responseTypes);
    }

    private static IReadOnlyList<ValidationResult> Validate(AiQueryRequest request)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(
            request,
            new ValidationContext(request),
            results,
            validateAllProperties: true);
        return results;
    }

    private static AiQuestionUnderstanding CreateUnderstanding(
        AiAnalysisType analysis) =>
        new(
            analysis,
            Period: null,
            From: null,
            To: null,
            ProductName: null,
            CategoryName: null,
            Granularity: null,
            Metric: null,
            Limit: null);

    private sealed class StubCoordinator(
        Func<string, CancellationToken, Task<AiAnalyticsExecutionResult>> execute)
        : IAiAnalyticsCoordinator
    {
        public string? LastQuestion { get; private set; }

        public Task<AiAnalyticsExecutionResult> ExecuteAsync(
            string question,
            CancellationToken cancellationToken = default)
        {
            LastQuestion = question;
            return execute(question, cancellationToken);
        }
    }
}
