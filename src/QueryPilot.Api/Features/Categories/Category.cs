using QueryPilot.Api.Features.Products;

namespace QueryPilot.Api.Features.Categories;

public sealed class Category
{
    public const int NameMaxLength = 100;

    public long Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public bool IsActive { get; set; }

    public DateTime CreatedAt { get; set; }

    public ICollection<Product> Products { get; } = [];
}
