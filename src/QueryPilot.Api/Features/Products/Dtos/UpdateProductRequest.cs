using System.ComponentModel.DataAnnotations;

namespace QueryPilot.Api.Features.Products.Dtos;

public sealed class UpdateProductRequest
{
    [Required(AllowEmptyStrings = false)]
    [StringLength(Product.NameMaxLength, MinimumLength = 1)]
    public string Name { get; init; } = string.Empty;

    [Required(AllowEmptyStrings = false)]
    [StringLength(Product.SkuMaxLength, MinimumLength = 1)]
    public string SKU { get; init; } = string.Empty;

    [Range(1, long.MaxValue)]
    public long CategoryId { get; init; }

    [Range(
        typeof(decimal),
        "0.01",
        "9999999999999999.99",
        ParseLimitsInInvariantCulture = true,
        ConvertValueInInvariantCulture = true)]
    public decimal UnitPrice { get; init; }

    [Required]
    public bool? IsActive { get; init; }
}
