namespace QueryPilot.Api.Features.Customers;

public sealed class Customer
{
    public const int NameMaxLength = 150;
    public const int EmailMaxLength = 320;
    public const int CityMaxLength = 100;

    public long Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string NormalizedEmail { get; set; } = string.Empty;

    public string City { get; set; } = string.Empty;

    public bool IsActive { get; set; }

    public DateTime CreatedAt { get; set; }
}
