using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using QueryPilot.Api.Common.Exceptions;
using QueryPilot.Api.Data;
using QueryPilot.Api.Features.Analytics;

namespace QueryPilot.Api.Features.Ai;

public sealed partial class AiIntentValidator(
    AppDbContext dbContext,
    TimeProvider timeProvider) : IAiIntentValidator
{
    private static readonly CultureInfo TurkishCulture = CultureInfo.GetCultureInfo("tr-TR");

    public async Task<ValidatedAnalyticsIntent> ValidateAsync(
        AiQuestionUnderstanding intent,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(intent);

        var errors = new Dictionary<string, string[]>();

        if (!Enum.IsDefined(intent.Analysis)
            || intent.Analysis == AiAnalysisType.Unknown)
        {
            errors[nameof(intent.Analysis)] =
                ["A supported analysis type is required."];
        }

        var range = ResolveDateRange(intent, errors);
        var granularity = ValidateGranularity(intent, errors);
        var metric = ValidateMetric(intent, errors);
        var limit = ValidateLimit(intent, errors);
        var productName = NormalizeOptional(intent.ProductName);
        var categoryName = NormalizeOptional(intent.CategoryName);
        long? categoryId = null;

        if (categoryName is not null)
        {
            categoryId = await dbContext.Categories
                .AsNoTracking()
                .Where(category => category.Name == categoryName)
                .Select(category => (long?)category.Id)
                .SingleOrDefaultAsync(cancellationToken);

            if (categoryId is null)
            {
                errors[nameof(intent.CategoryName)] =
                    ["The requested category does not exist."];
            }
        }

        if (errors.Count > 0 || range is null)
        {
            throw new RequestValidationException(
                errors,
                "The AI analytics intent is invalid.");
        }

        return new ValidatedAnalyticsIntent(
            intent.Analysis,
            range.Value.FromUtc,
            range.Value.ToUtc,
            productName,
            categoryId,
            categoryName,
            granularity,
            metric,
            limit);
    }

    private (DateTimeOffset FromUtc, DateTimeOffset ToUtc)? ResolveDateRange(
        AiQuestionUnderstanding intent,
        IDictionary<string, string[]> errors)
    {
        var period = NormalizeOptional(intent.Period);
        var fromText = NormalizeOptional(intent.From);
        var toText = NormalizeOptional(intent.To);

        if (period is not null && (fromText is not null || toText is not null))
        {
            errors[nameof(intent.Period)] =
                ["Period cannot be combined with explicit date boundaries."];
            return null;
        }

        DateTimeOffset fromUtc;
        DateTimeOffset toUtc;

        if (period is not null)
        {
            var resolvedPeriod = ResolvePeriod(period);
            if (resolvedPeriod is null)
            {
                errors[nameof(intent.Period)] =
                    ["The requested period is not supported."];
                return null;
            }

            (fromUtc, toUtc) = resolvedPeriod.Value;
        }
        else
        {
            if (fromText is null || toText is null)
            {
                errors[nameof(intent.From)] =
                    ["A period or both From and To dates are required."];
                errors[nameof(intent.To)] =
                    ["A period or both From and To dates are required."];
                return null;
            }

            if (!TryParseUtc(fromText, out fromUtc))
            {
                errors[nameof(intent.From)] =
                    ["From must be a valid ISO-8601 date or timestamp."];
            }

            if (!TryParseUtc(toText, out toUtc))
            {
                errors[nameof(intent.To)] =
                    ["To must be a valid ISO-8601 date or timestamp."];
            }

            if (errors.ContainsKey(nameof(intent.From))
                || errors.ContainsKey(nameof(intent.To)))
            {
                return null;
            }
        }

        if (fromUtc >= toUtc)
        {
            errors[nameof(intent.From)] = ["From must be earlier than To."];
            errors[nameof(intent.To)] = ["To must be later than From."];
            return null;
        }

        if (ExceedsMaximumRange(fromUtc, toUtc))
        {
            errors[nameof(intent.To)] =
                [$"The analytics date range cannot exceed {AnalyticsService.MaximumRangeInYears} years."];
            return null;
        }

        return (fromUtc, toUtc);
    }

    private (DateTimeOffset FromUtc, DateTimeOffset ToUtc)? ResolvePeriod(
        string period)
    {
        var normalized = MultipleWhitespaceRegex()
            .Replace(period.Trim().ToLower(TurkishCulture), " ");
        var now = timeProvider.GetUtcNow();
        var today = new DateTimeOffset(
            now.Year,
            now.Month,
            now.Day,
            0,
            0,
            0,
            TimeSpan.Zero);

        var countedPeriod = LastCountedPeriodRegex().Match(normalized);
        if (countedPeriod.Success
            && int.TryParse(
                countedPeriod.Groups["count"].Value,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out var count)
            && count is > 0 and <= 10_000)
        {
            try
            {
                var from = countedPeriod.Groups["unit"].Value switch
                {
                    "gün" => now.AddDays(-count),
                    "hafta" => now.AddDays(-7 * count),
                    "ay" => now.AddMonths(-count),
                    "yıl" => now.AddYears(-count),
                    _ => (DateTimeOffset?)null
                };

                return from is null ? null : (from.Value, now);
            }
            catch (ArgumentOutOfRangeException)
            {
                return null;
            }
        }

        return normalized switch
        {
            "bugün" => (today, now),
            "son gün" => (now.AddDays(-1), now),
            "son hafta" => (now.AddDays(-7), now),
            "son ay" => (now.AddMonths(-1), now),
            "son yıl" => (now.AddYears(-1), now),
            "bu hafta" => (StartOfWeek(today), now),
            "bu ay" => (StartOfMonth(today), now),
            "bu yıl" => (StartOfYear(today), now),
            "geçen hafta" => PreviousWeek(today),
            "geçen ay" => PreviousMonth(today),
            "geçen yıl" => PreviousYear(today),
            _ => null
        };
    }

    private static AnalyticsGranularity? ValidateGranularity(
        AiQuestionUnderstanding intent,
        IDictionary<string, string[]> errors)
    {
        if (intent.Analysis == AiAnalysisType.SalesTrend)
        {
            if (intent.Granularity is null
                || !Enum.IsDefined(intent.Granularity.Value))
            {
                errors[nameof(intent.Granularity)] =
                    ["Sales trend requires Daily, Weekly, or Monthly granularity."];
                return null;
            }

            return intent.Granularity.Value switch
            {
                AiGranularity.Daily => AnalyticsGranularity.Daily,
                AiGranularity.Weekly => AnalyticsGranularity.Weekly,
                AiGranularity.Monthly => AnalyticsGranularity.Monthly,
                _ => null
            };
        }

        if (intent.Granularity is not null)
        {
            errors[nameof(intent.Granularity)] =
                ["Granularity is supported only for sales trend."];
        }

        return null;
    }

    private static TopProductsMetric? ValidateMetric(
        AiQuestionUnderstanding intent,
        IDictionary<string, string[]> errors)
    {
        if (intent.Analysis == AiAnalysisType.TopProducts)
        {
            if (intent.Metric is null || !Enum.IsDefined(intent.Metric.Value))
            {
                errors[nameof(intent.Metric)] =
                    ["Top products requires Quantity or Revenue metric."];
                return null;
            }

            return intent.Metric.Value switch
            {
                AiTopProductsMetric.Quantity => TopProductsMetric.Quantity,
                AiTopProductsMetric.Revenue => TopProductsMetric.Revenue,
                _ => null
            };
        }

        if (intent.Metric is not null)
        {
            errors[nameof(intent.Metric)] =
                ["Metric is supported only for top products."];
        }

        return null;
    }

    private static int? ValidateLimit(
        AiQuestionUnderstanding intent,
        IDictionary<string, string[]> errors)
    {
        if (intent.Analysis != AiAnalysisType.TopProducts)
        {
            if (intent.Limit is not null)
            {
                errors[nameof(intent.Limit)] =
                    ["Limit is supported only for top products."];
            }

            return null;
        }

        var limit = intent.Limit ?? AnalyticsService.DefaultTopProductsLimit;
        if (limit <= 0 || limit > AnalyticsService.MaximumTopProductsLimit)
        {
            errors[nameof(intent.Limit)] =
                [$"Limit must be between 1 and {AnalyticsService.MaximumTopProductsLimit}."];
        }

        return limit;
    }

    private static bool TryParseUtc(string value, out DateTimeOffset result) =>
        DateTimeOffset.TryParse(
            value,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AllowWhiteSpaces
                | DateTimeStyles.AssumeUniversal
                | DateTimeStyles.AdjustToUniversal,
            out result);

    private static bool ExceedsMaximumRange(
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc)
    {
        try
        {
            return toUtc > fromUtc.AddYears(AnalyticsService.MaximumRangeInYears);
        }
        catch (ArgumentOutOfRangeException)
        {
            return true;
        }
    }

    private static DateTimeOffset StartOfWeek(DateTimeOffset date)
    {
        var daysSinceMonday = ((int)date.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7;
        return date.AddDays(-daysSinceMonday);
    }

    private static DateTimeOffset StartOfMonth(DateTimeOffset date) =>
        new(date.Year, date.Month, 1, 0, 0, 0, TimeSpan.Zero);

    private static DateTimeOffset StartOfYear(DateTimeOffset date) =>
        new(date.Year, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static (DateTimeOffset, DateTimeOffset) PreviousWeek(DateTimeOffset date)
    {
        var currentWeek = StartOfWeek(date);
        return (currentWeek.AddDays(-7), currentWeek);
    }

    private static (DateTimeOffset, DateTimeOffset) PreviousMonth(DateTimeOffset date)
    {
        var currentMonth = StartOfMonth(date);
        return (currentMonth.AddMonths(-1), currentMonth);
    }

    private static (DateTimeOffset, DateTimeOffset) PreviousYear(DateTimeOffset date)
    {
        var currentYear = StartOfYear(date);
        return (currentYear.AddYears(-1), currentYear);
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    [GeneratedRegex(@"\s+")]
    private static partial Regex MultipleWhitespaceRegex();

    [GeneratedRegex(@"^son\s+(?<count>\d+)\s+(?<unit>gün|hafta|ay|yıl)$")]
    private static partial Regex LastCountedPeriodRegex();
}
