using System.ComponentModel.DataAnnotations;

namespace QueryPilot.Api.Features.Customers.Dtos;

public sealed class UpdateCustomerRequest
{
    [Required(AllowEmptyStrings = false)]
    [StringLength(Customer.NameMaxLength, MinimumLength = 1)]
    public string Name { get; init; } = string.Empty;

    [Required(AllowEmptyStrings = false)]
    [StringLength(Customer.EmailMaxLength)]
    [EmailAddress]
    public string Email { get; init; } = string.Empty;

    [Required(AllowEmptyStrings = false)]
    [StringLength(Customer.CityMaxLength, MinimumLength = 1)]
    public string City { get; init; } = string.Empty;

    [Required]
    public bool? IsActive { get; init; }
}
