using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using QueryPilot.Api.Common.ErrorHandling;
using QueryPilot.Api.Common.Exceptions;
using QueryPilot.Api.Features.Analytics;
using QueryPilot.Api.Features.Analytics.Dtos;
using Xunit;

namespace QueryPilot.Api.Tests.Features.Ai;

public sealed class AiResilienceTests
{
    private static readonly DateTimeOffset FromUtc =
        new(2026, 7, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset ToUtc =
        new(2026, 8, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Ai_provider_exception_returns_safe_503_without_internal_details()
    {
        const string sensitiveDetail = "key-sk-secret provider-body-secret";
        var context = new DefaultHttpContext();
        context.Request.Path = "/api/ai/query";
        context.Response.Body = new MemoryStream();
        var handler = new GlobalExceptionHandler(
            NullLogger<GlobalExceptionHandler>.Instance);

        var handled = await handler.TryHandleAsync(
            context,
            new AiServiceUnavailableException(sensitiveDetail),
            CancellationToken.None);

        Assert.True(handled);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, context.Response.StatusCode);
        context.Response.Body.Position = 0;
        using var document = await JsonDocument.ParseAsync(context.Response.Body);
        var responseText = document.RootElement.ToString();
        Assert.Contains("temporarily unavailable", responseText);
        Assert.DoesNotContain("key-sk-secret", responseText);
        Assert.DoesNotContain("provider-body-secret", responseText);
    }

    [Fact]
    public async Task Direct_analytics_controller_works_without_an_ai_provider()
    {
        var summary = CreateSummary();
        var controller = new AnalyticsController(new StubAnalyticsService(summary));
        var request = new AnalyticsDateRangeRequest
        {
            From = FromUtc,
            To = ToUtc
        };

        var actionResult = await controller.GetSummary(request, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(actionResult.Result);
        Assert.Same(summary, okResult.Value);
    }

    private static SalesSummaryResponse CreateSummary() =>
        new(
            FromUtc,
            ToUtc,
            TotalRevenue: 125000m,
            OrderCount: 5,
            UnitsSold: 8,
            AverageOrderValue: 25000m,
            new ComparisonPeriodResponse(
                FromUtc,
                ToUtc,
                FromUtc.AddMonths(-1),
                FromUtc,
                (ToUtc - FromUtc).Ticks,
                "Adjacent half-open UTC ranges with equal tick duration."),
            new MetricComparisonResponse(
                CurrentValue: 125000m,
                PreviousValue: 100000m,
                PercentageChange: 25m,
                ComparisonStatus.Increase));

    private sealed class StubAnalyticsService(SalesSummaryResponse summary)
        : IAnalyticsService
    {
        public AnalyticsDateRange CreateDateRange(DateTimeOffset from, DateTimeOffset to) =>
            throw new NotSupportedException();

        public Task<SalesSummaryResponse> GetSalesSummaryAsync(
            DateTimeOffset from,
            DateTimeOffset to,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(summary);

        public Task<SalesTrendResponse> GetSalesTrendAsync(
            DateTimeOffset from,
            DateTimeOffset to,
            AnalyticsGranularity granularity,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<TopProductsResponse> GetTopProductsAsync(
            DateTimeOffset from,
            DateTimeOffset to,
            TopProductsMetric metric,
            int? limit = null,
            long? categoryId = null,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<CategoryPerformanceResponse> GetCategoryPerformanceAsync(
            DateTimeOffset from,
            DateTimeOffset to,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<ReturnAnalyticsResponse> GetReturnAnalyticsAsync(
            DateTimeOffset from,
            DateTimeOffset to,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
