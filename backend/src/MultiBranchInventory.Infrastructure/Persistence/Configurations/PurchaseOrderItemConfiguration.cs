using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MultiBranchInventory.Domain.Entities;

namespace MultiBranchInventory.Infrastructure.Persistence.Configurations;

public class PurchaseOrderItemConfiguration
    : IEntityTypeConfiguration<PurchaseOrderItem>
{
    public void Configure(EntityTypeBuilder<PurchaseOrderItem> builder)
    {
        builder.ToTable("PurchaseOrderItems");

        builder.HasKey(item => item.Id);

        builder.Property(item => item.OrderedQuantity)
            .IsRequired();

        builder.Property(item => item.UnitPrice)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.HasIndex(item => new
        {
            item.PurchaseOrderId,
            item.PurchaseRequestItemId
        }).IsUnique();

        builder.HasOne(item => item.PurchaseOrder)
            .WithMany(order => order.Items)
            .HasForeignKey(item => item.PurchaseOrderId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(item => item.PurchaseRequestItem)
            .WithMany(requestItem => requestItem.PurchaseOrderItems)
            .HasForeignKey(item => item.PurchaseRequestItemId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(item => item.Product)
            .WithMany(product => product.PurchaseOrderItems)
            .HasForeignKey(item => item.ProductId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}