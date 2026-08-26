using MultiBranchInventory.Domain.Common;
using MultiBranchInventory.Domain.Enums;

namespace MultiBranchInventory.Domain.Entities;

public class InventoryTransaction : BaseEntity
{
    public Guid InventoryId { get; set; }

    public InventoryTransactionType Type { get; set; }

    public int Quantity { get; set; }

    public int QuantityBefore { get; set; }

    public int QuantityAfter { get; set; }

    public string? ReferenceNumber { get; set; }

    public string? Notes { get; set; }

    public Guid PerformedByUserId { get; set; }

    public Inventory Inventory { get; set; } = null!;

    public User PerformedByUser { get; set; } = null!;
}