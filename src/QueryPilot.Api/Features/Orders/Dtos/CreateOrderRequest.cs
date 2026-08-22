using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace QueryPilot.Api.Features.Orders.Dtos;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class CreateOrderRequest : IValidatableObject
{
    [Range(1, long.MaxValue)]
    public long CustomerId { get; init; }

    [Required]
    [MinLength(1)]
    public IReadOnlyList<CreateOrderItemRequest> Items { get; init; } = [];

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Items is null)
        {
            yield break;
        }

        var duplicateProductIds = Items
            .Where(item => item is not null && item.ProductId > 0)
            .GroupBy(item => item.ProductId)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .Order()
            .ToArray();

        if (duplicateProductIds.Length > 0)
        {
            yield return new ValidationResult(
                $"Each product can appear only once. Duplicate product IDs: {string.Join(", ", duplicateProductIds)}.",
                [nameof(Items)]);
        }
    }
}
