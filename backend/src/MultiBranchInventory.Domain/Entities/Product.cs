using MultiBranchInventory.Domain.Common;

namespace MultiBranchInventory.Domain.Entities;

public class Product : BaseEntity
{
    public string Sku { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public Guid CategoryId { get; set; }

    public decimal UnitPrice { get; set; }

    public bool IsActive { get; set; } = true;

    public Category Category { get; set; } = null!;

    public ICollection<Inventory> Inventories { get; set; } = new List<Inventory>();
}