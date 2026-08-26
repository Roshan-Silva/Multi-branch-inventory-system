using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MultiBranchInventory.Domain.Entities;

namespace MultiBranchInventory.Infrastructure.Persistence.Configurations;

public class InventoryTransactionConfiguration
    : IEntityTypeConfiguration<InventoryTransaction>
{
    public void Configure(EntityTypeBuilder<InventoryTransaction> builder)
    {
        builder.ToTable("InventoryTransactions");

        builder.HasKey(transaction => transaction.Id);

        builder.Property(transaction => transaction.Type)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(transaction => transaction.Quantity)
            .IsRequired();

        builder.Property(transaction => transaction.QuantityBefore)
            .IsRequired();

        builder.Property(transaction => transaction.QuantityAfter)
            .IsRequired();

        builder.Property(transaction => transaction.ReferenceNumber)
            .HasMaxLength(100);

        builder.Property(transaction => transaction.Notes)
            .HasMaxLength(1000);

        builder.HasOne(transaction => transaction.Inventory)
            .WithMany(inventory => inventory.Transactions)
            .HasForeignKey(transaction => transaction.InventoryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(transaction => transaction.PerformedByUser)
            .WithMany(user => user.InventoryTransactions)
            .HasForeignKey(transaction => transaction.PerformedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}