using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using QueryPilot.Api.Features.Customers;

namespace QueryPilot.Api.Data.Configurations;

public sealed class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> builder)
    {
        builder.HasKey(customer => customer.Id);

        builder.Property(customer => customer.Name)
            .IsRequired()
            .HasMaxLength(Customer.NameMaxLength);

        builder.Property(customer => customer.Email)
            .IsRequired()
            .HasMaxLength(Customer.EmailMaxLength);

        builder.Property(customer => customer.NormalizedEmail)
            .IsRequired()
            .HasMaxLength(Customer.EmailMaxLength);

        builder.Property(customer => customer.City)
            .IsRequired()
            .HasMaxLength(Customer.CityMaxLength);

        builder.Property(customer => customer.IsActive)
            .IsRequired();

        builder.Property(customer => customer.CreatedAt)
            .IsRequired();

        builder.HasIndex(customer => customer.NormalizedEmail)
            .IsUnique();
    }
}
