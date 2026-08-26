using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MultiBranchInventory.Domain.Entities;

namespace MultiBranchInventory.Infrastructure.Persistence.Configurations;

public class GoodsReceivedNoteConfiguration
    : IEntityTypeConfiguration<GoodsReceivedNote>
{
    public void Configure(EntityTypeBuilder<GoodsReceivedNote> builder)
    {
        builder.ToTable("GoodsReceivedNotes");

        builder.HasKey(grn => grn.Id);

        builder.Property(grn => grn.GrnNumber)
            .IsRequired()
            .HasMaxLength(30);

        builder.HasIndex(grn => grn.GrnNumber)
            .IsUnique();

        builder.Property(grn => grn.DeliveryReference)
            .HasMaxLength(100);

        builder.Property(grn => grn.Notes)
            .HasMaxLength(1000);

        builder.Property(grn => grn.Status)
            .HasConversion<int>()
            .IsRequired();

        builder.HasOne(grn => grn.PurchaseOrder)
            .WithMany(order => order.GoodsReceivedNotes)
            .HasForeignKey(grn => grn.PurchaseOrderId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(grn => grn.ReceivedByUser)
            .WithMany(user => user.ReceivedGoodsNotes)
            .HasForeignKey(grn => grn.ReceivedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(grn => grn.ConfirmedByUser)
            .WithMany(user => user.ConfirmedGoodsNotes)
            .HasForeignKey(grn => grn.ConfirmedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}