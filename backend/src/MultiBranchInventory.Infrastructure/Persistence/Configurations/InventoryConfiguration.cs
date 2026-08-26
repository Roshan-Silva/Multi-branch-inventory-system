using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MultiBranchInventory.Domain.Entities;

namespace MultiBranchInventory.Infrastructure.Persistence.Configurations;

public class InventoryConfiguration : IEntityTypeConfiguration<Inventory>
{
    public void Configure(EntityTypeBuilder<Inventory> builder)
    {
        builder.ToTable("Inventories");

        builder.HasKey(inventory => inventory.Id);

        builder.Property(inventory => inventory.QuantityOnHand)
            .IsRequired();

        builder.Property(inventory => inventory.MinimumStockLevel)
            .IsRequired();

        builder.Property(inventory => inventory.ReorderLevel)
            .IsRequired();

        builder.HasIndex(inventory => new
        {
            inventory.BranchId,
            inventory.ProductId
        })
        .IsUnique();

        builder.HasOne(inventory => inventory.Branch)
            .WithMany(branch => branch.Inventories)
            .HasForeignKey(inventory => inventory.BranchId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(inventory => inventory.Product)
            .WithMany(product => product.Inventories)
            .HasForeignKey(inventory => inventory.ProductId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}