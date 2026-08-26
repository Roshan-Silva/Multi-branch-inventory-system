using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MultiBranchInventory.Domain.Entities;

namespace MultiBranchInventory.Infrastructure.Persistence.Configurations;

public class SupplierConfiguration : IEntityTypeConfiguration<Supplier>
{
    public void Configure(EntityTypeBuilder<Supplier> builder)
    {
        builder.ToTable("Suppliers");

        builder.HasKey(supplier => supplier.Id);

        builder.Property(supplier => supplier.Code)
            .IsRequired()
            .HasMaxLength(20);

        builder.HasIndex(supplier => supplier.Code)
            .IsUnique();

        builder.Property(supplier => supplier.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(supplier => supplier.ContactPerson)
            .HasMaxLength(150);

        builder.Property(supplier => supplier.Email)
            .HasMaxLength(150);

        builder.Property(supplier => supplier.PhoneNumber)
            .HasMaxLength(30);

        builder.Property(supplier => supplier.Address)
            .HasMaxLength(500);

        builder.Property(supplier => supplier.IsActive)
            .IsRequired();
    }
}