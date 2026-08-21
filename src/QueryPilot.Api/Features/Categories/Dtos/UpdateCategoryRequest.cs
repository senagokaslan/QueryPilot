using System.ComponentModel.DataAnnotations;

namespace QueryPilot.Api.Features.Categories.Dtos;

public sealed class UpdateCategoryRequest
{
    [Required(AllowEmptyStrings = false)]
    [StringLength(Category.NameMaxLength, MinimumLength = 1)]
    public string Name { get; init; } = string.Empty;

    [Required]
    public bool? IsActive { get; init; }
}
