using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace QueryPilot.Api.Features.Ai;

public static partial class AiExplanationGuard
{
    public const int MaximumLength = 500;

    public static AiExplanationResponse Validate(
        AiResultExplanation explanation,
        string analyticsResultJson)
    {
        if (string.IsNullOrWhiteSpace(explanation.Text)
            || explanation.Text.Length > MaximumLength)
        {
            return AiExplanationResponse.RejectedUnsafe();
        }

        var allowedNumbers = ReadJsonNumbers(analyticsResultJson);
        var containsUnsupportedNumber = NumberRegex()
            .Matches(explanation.Text)
            .Select(match => NormalizeNumber(match.Value))
            .Any(number => number is null || !allowedNumbers.Contains(number.Value));

        return containsUnsupportedNumber
            ? AiExplanationResponse.RejectedUnsafe()
            : AiExplanationResponse.Available(explanation.Text.Trim());
    }

    private static HashSet<decimal> ReadJsonNumbers(string json)
    {
        using var document = JsonDocument.Parse(json);
        var numbers = new HashSet<decimal>();
        AddNumbers(document.RootElement, numbers);
        return numbers;
    }

    private static void AddNumbers(JsonElement element, ISet<decimal> numbers)
    {
        if (element.ValueKind == JsonValueKind.Number
            && element.TryGetDecimal(out var value))
        {
            numbers.Add(value);
            return;
        }

        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                AddNumbers(property.Value, numbers);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                AddNumbers(item, numbers);
            }
        }
    }

    private static decimal? NormalizeNumber(string text)
    {
        var value = text.Trim().TrimEnd('%').Trim();
        var commaCount = value.Count(character => character == ',');
        var dotCount = value.Count(character => character == '.');

        if (commaCount > 0 && dotCount > 0)
        {
            var decimalSeparator = value.LastIndexOf(',') > value.LastIndexOf('.')
                ? ','
                : '.';
            var groupSeparator = decimalSeparator == ',' ? "." : ",";
            value = value.Replace(groupSeparator, string.Empty, StringComparison.Ordinal)
                .Replace(decimalSeparator, '.');
        }
        else if (commaCount > 0)
        {
            value = NormalizeSingleSeparator(value, ',', commaCount);
        }
        else if (dotCount > 0)
        {
            value = NormalizeSingleSeparator(value, '.', dotCount);
        }

        return decimal.TryParse(
            value,
            NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
            CultureInfo.InvariantCulture,
            out var parsed)
            ? parsed
            : null;
    }

    private static string NormalizeSingleSeparator(
        string value,
        char separator,
        int separatorCount)
    {
        var lastSeparatorIndex = value.LastIndexOf(separator);
        var digitsAfterSeparator = value.Length - lastSeparatorIndex - 1;
        var isGrouping = separatorCount > 1 || digitsAfterSeparator == 3;

        return isGrouping
            ? value.Replace(separator.ToString(), string.Empty, StringComparison.Ordinal)
            : value.Replace(separator, '.');
    }

    [GeneratedRegex(@"(?<![\p{L}\p{N}])[-+]?\d+(?:[.,]\d+)*(?:\s*%)?", RegexOptions.CultureInvariant)]
    private static partial Regex NumberRegex();
}
