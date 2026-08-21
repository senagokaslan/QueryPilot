using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using QueryPilot.Api.Features.Categories;

namespace QueryPilot.Api.Data.Configurations;

public sealed class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> builder)
    {
        builder.HasKey(category => category.Id);

        builder.Property(category => category.Name)
            .IsRequired()
            .HasColumnType("citext")
            .HasMaxLength(Category.NameMaxLength);

        builder.Property(category => category.IsActive)
            .IsRequired();

        builder.Property(category => category.CreatedAt)
            .IsRequired();

        builder.HasIndex(category => category.Name)
            .IsUnique();
    }
}
