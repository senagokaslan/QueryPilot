using System.ComponentModel.DataAnnotations;
using QueryPilot.Api.Common.Pagination;

namespace QueryPilot.Api.Features.Orders.Dtos;

public sealed class OrderListRequest : IValidatableObject
{
    [Range(1, int.MaxValue)]
    public int Page { get; init; } = 1;

    [PageSize]
    public int? PageSize { get; init; }

    public DateTimeOffset? From { get; init; }

    public DateTimeOffset? To { get; init; }

    public OrderStatus? Status { get; init; }

    [Range(1, long.MaxValue)]
    public long? CustomerId { get; init; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (From.HasValue && To.HasValue && From.Value > To.Value)
        {
            yield return new ValidationResult(
                "From must be earlier than or equal to To.",
                [nameof(From), nameof(To)]);
        }
    }
}
