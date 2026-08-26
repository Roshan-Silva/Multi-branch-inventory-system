using MultiBranchInventory.Domain.Common;

namespace MultiBranchInventory.Domain.Entities;

public class Inventory : BaseEntity
{
    public Guid BranchId { get; set; }

    public Guid ProductId { get; set; }

    public int QuantityOnHand { get; set; }

    public int MinimumStockLevel { get; set; }

    public int ReorderLevel { get; set; }
}