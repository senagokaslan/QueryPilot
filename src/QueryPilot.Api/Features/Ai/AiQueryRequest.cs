using System.ComponentModel.DataAnnotations;

namespace QueryPilot.Api.Features.Ai;

/// <summary>Natural-language analytics question submitted to QueryPilot.</summary>
public sealed class AiQueryRequest : IValidatableObject
{
    public const int QuestionMaximumLength = 2000;

    /// <summary>
    /// Turkish natural-language BI question, including the requested period and
    /// analysis-specific details where applicable.
    /// </summary>
    /// <example>Son 3 ayın aylık satış trendini göster.</example>
    [Required(AllowEmptyStrings = false)]
    [StringLength(QuestionMaximumLength, MinimumLength = 1)]
    public string Question { get; init; } = string.Empty;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!string.IsNullOrEmpty(Question) && string.IsNullOrWhiteSpace(Question))
        {
            yield return new ValidationResult(
                "Question cannot contain only whitespace.",
                [nameof(Question)]);
        }
    }
}
