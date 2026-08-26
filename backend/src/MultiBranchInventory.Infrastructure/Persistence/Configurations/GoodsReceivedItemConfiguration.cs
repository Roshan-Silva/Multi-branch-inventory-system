using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MultiBranchInventory.Domain.Entities;

namespace MultiBranchInventory.Infrastructure.Persistence.Configurations;

public class GoodsReceivedItemConfiguration
    : IEntityTypeConfiguration<GoodsReceivedItem>
{
    public void Configure(EntityTypeBuilder<GoodsReceivedItem> builder)
    {
        builder.ToTable("GoodsReceivedItems");

        builder.HasKey(item => item.Id);

        builder.Property(item => item.ReceivedQuantity)
            .IsRequired();

        builder.Property(item => item.Notes)
            .HasMaxLength(500);

        builder.HasIndex(item => new
        {
            item.GoodsReceivedNoteId,
            item.PurchaseOrderItemId
        }).IsUnique();

        builder.HasOne(item => item.GoodsReceivedNote)
            .WithMany(grn => grn.Items)
            .HasForeignKey(item => item.GoodsReceivedNoteId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(item => item.PurchaseOrderItem)
            .WithMany(orderItem => orderItem.GoodsReceivedItems)
            .HasForeignKey(item => item.PurchaseOrderItemId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}