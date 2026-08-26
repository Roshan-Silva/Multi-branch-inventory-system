using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MultiBranchInventory.Domain.Entities;

namespace MultiBranchInventory.Infrastructure.Persistence.Configurations;

public class PurchaseRequestConfiguration
    : IEntityTypeConfiguration<PurchaseRequest>
{
    public void Configure(EntityTypeBuilder<PurchaseRequest> builder)
    {
        builder.ToTable("PurchaseRequests");

        builder.HasKey(request => request.Id);

        builder.Property(request => request.RequestNumber)
            .IsRequired()
            .HasMaxLength(30);

        builder.HasIndex(request => request.RequestNumber)
            .IsUnique();

        builder.Property(request => request.Reason)
            .HasMaxLength(1000);

        builder.Property(request => request.RejectionReason)
            .HasMaxLength(1000);

        builder.Property(request => request.Status)
            .HasConversion<int>()
            .IsRequired();

        builder.HasOne(request => request.Branch)
            .WithMany(branch => branch.PurchaseRequests)
            .HasForeignKey(request => request.BranchId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(request => request.RequestedByUser)
            .WithMany(user => user.RequestedPurchaseRequests)
            .HasForeignKey(request => request.RequestedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(request => request.ReviewedByUser)
            .WithMany(user => user.ReviewedPurchaseRequests)
            .HasForeignKey(request => request.ReviewedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}