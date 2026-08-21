using System.ComponentModel.DataAnnotations;

namespace QueryPilot.Api.Features.Products.Dtos;

public sealed class ProductListRequest
{
    [Range(1, int.MaxValue)]
    public int Page { get; init; } = 1;

    [Range(1, int.MaxValue)]
    public int? PageSize { get; init; }

    public bool? IsActive { get; init; }

    [Range(1, long.MaxValue)]
    public long? CategoryId { get; init; }

    [StringLength(Product.NameMaxLength)]
    public string? Search { get; init; }
}
