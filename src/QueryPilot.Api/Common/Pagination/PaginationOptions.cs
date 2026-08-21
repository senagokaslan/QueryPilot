using System.ComponentModel.DataAnnotations;

namespace QueryPilot.Api.Common.Pagination;

public sealed class PaginationOptions
{
    public const string SectionName = "Pagination";

    [Range(1, 1_000)]
    public int DefaultPageSize { get; set; } = 20;

    [Range(1, 1_000)]
    public int MaxPageSize { get; set; } = 100;
}
