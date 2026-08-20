using QueryPilot.Api.Features.Categories;

namespace QueryPilot.Api.Features.Products;

public sealed class Product
{
    public const int NameMaxLength = 200;
    public const int SkuMaxLength = 64;

    public long Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string SKU { get; set; } = string.Empty;

    public long CategoryId { get; set; }

    public decimal UnitPrice { get; set; }

    public bool IsActive { get; set; }

    public DateTime CreatedAt { get; set; }

    public Category Category { get; set; } = null!;
}
