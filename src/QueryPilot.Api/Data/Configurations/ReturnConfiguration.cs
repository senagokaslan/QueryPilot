using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using QueryPilot.Api.Features.Returns;

namespace QueryPilot.Api.Data.Configurations;

public sealed class ReturnConfiguration : IEntityTypeConfiguration<Return>
{
    public void Configure(EntityTypeBuilder<Return> builder)
    {
        builder.HasKey(returnRecord => returnRecord.Id);

        builder.Property(returnRecord => returnRecord.Quantity)
            .IsRequired();

        builder.Property(returnRecord => returnRecord.Reason)
            .IsRequired()
            .HasMaxLength(Return.ReasonMaxLength);

        builder.Property(returnRecord => returnRecord.ReturnDate)
            .IsRequired();

        builder.Property(returnRecord => returnRecord.Amount)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.HasIndex(returnRecord => returnRecord.OrderItemId);
        builder.HasIndex(returnRecord => returnRecord.ReturnDate);

        builder.HasOne(returnRecord => returnRecord.OrderItem)
            .WithMany(orderItem => orderItem.Returns)
            .HasForeignKey(returnRecord => returnRecord.OrderItemId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
