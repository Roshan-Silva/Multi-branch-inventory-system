using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MultiBranchInventory.Domain.Entities;

namespace MultiBranchInventory.Infrastructure.Persistence.Configurations;

public class PurchaseRequestItemConfiguration
    : IEntityTypeConfiguration<PurchaseRequestItem>
{
    public void Configure(EntityTypeBuilder<PurchaseRequestItem> builder)
    {
        builder.ToTable("PurchaseRequestItems");

        builder.HasKey(item => item.Id);

        builder.Property(item => item.RequestedQuantity)
            .IsRequired();

        builder.Property(item => item.Notes)
            .HasMaxLength(500);

        builder.HasIndex(item => new
        {
            item.PurchaseRequestId,
            item.ProductId
        }).IsUnique();

        builder.HasOne(item => item.PurchaseRequest)
            .WithMany(request => request.Items)
            .HasForeignKey(item => item.PurchaseRequestId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(item => item.Product)
            .WithMany(product => product.PurchaseRequestItems)
            .HasForeignKey(item => item.ProductId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}