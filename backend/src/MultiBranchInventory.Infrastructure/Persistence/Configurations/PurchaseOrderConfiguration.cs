using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MultiBranchInventory.Domain.Entities;

namespace MultiBranchInventory.Infrastructure.Persistence.Configurations;

public class PurchaseOrderConfiguration
    : IEntityTypeConfiguration<PurchaseOrder>
{
    public void Configure(EntityTypeBuilder<PurchaseOrder> builder)
    {
        builder.ToTable("PurchaseOrders");

        builder.HasKey(order => order.Id);

        builder.Property(order => order.OrderNumber)
            .IsRequired()
            .HasMaxLength(30);

        builder.HasIndex(order => order.OrderNumber)
            .IsUnique();

        builder.Property(order => order.Status)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(order => order.Notes)
            .HasMaxLength(1000);

        builder.HasOne(order => order.PurchaseRequest)
            .WithMany(request => request.PurchaseOrders)
            .HasForeignKey(order => order.PurchaseRequestId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(order => order.Supplier)
            .WithMany(supplier => supplier.PurchaseOrders)
            .HasForeignKey(order => order.SupplierId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(order => order.Branch)
            .WithMany(branch => branch.PurchaseOrders)
            .HasForeignKey(order => order.BranchId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(order => order.CreatedByUser)
            .WithMany(user => user.CreatedPurchaseOrders)
            .HasForeignKey(order => order.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(order => order.ApprovedByUser)
            .WithMany(user => user.ApprovedPurchaseOrders)
            .HasForeignKey(order => order.ApprovedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}