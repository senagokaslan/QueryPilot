using System.ComponentModel.DataAnnotations;

namespace QueryPilot.Api.Features.Categories.Dtos;

public sealed class CategoryListRequest
{
    [Range(1, int.MaxValue)]
    public int Page { get; init; } = 1;

    [Range(1, int.MaxValue)]
    public int? PageSize { get; init; }

    public bool? IsActive { get; init; }
}
