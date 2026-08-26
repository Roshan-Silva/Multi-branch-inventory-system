using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MultiBranchInventory.Domain.Entities;

namespace MultiBranchInventory.Infrastructure.Persistence.Configurations;

public class BranchConfiguration : IEntityTypeConfiguration<Branch>
{
    public void Configure(EntityTypeBuilder<Branch> builder)
    {
        builder.ToTable("Branches");

        builder.HasKey(branch => branch.Id);

        builder.Property(branch => branch.Code)
            .IsRequired()
            .HasMaxLength(20);

        builder.HasIndex(branch => branch.Code)
            .IsUnique();

        builder.Property(branch => branch.Name)
            .IsRequired()
            .HasMaxLength(150);

        builder.Property(branch => branch.Address)
            .HasMaxLength(500);

        builder.Property(branch => branch.PhoneNumber)
            .HasMaxLength(30);

        builder.Property(branch => branch.Email)
            .HasMaxLength(150);

        builder.Property(branch => branch.IsActive)
            .IsRequired();
    }
}