using System.ComponentModel.DataAnnotations;
using QueryPilot.Api.Common.Pagination;

namespace QueryPilot.Api.Features.Customers.Dtos;

public sealed class CustomerListRequest
{
    [Range(1, int.MaxValue)]
    public int Page { get; init; } = 1;

    [PageSize]
    public int? PageSize { get; init; }

    [StringLength(Customer.EmailMaxLength)]
    public string? Search { get; init; }

    [StringLength(Customer.CityMaxLength)]
    public string? City { get; init; }

    public bool? IsActive { get; init; }
}
