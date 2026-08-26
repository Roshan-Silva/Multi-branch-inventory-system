using MultiBranchInventory.Domain.Common;

namespace MultiBranchInventory.Domain.Entities;

public class PurchaseRequestItem : BaseEntity
{
    public Guid PurchaseRequestId { get; set; }

    public Guid ProductId { get; set; }

    public int RequestedQuantity { get; set; }

    public string? Notes { get; set; }

    public PurchaseRequest PurchaseRequest { get; set; } = null!;

    public Product Product { get; set; } = null!;

    public ICollection<PurchaseOrderItem> PurchaseOrderItems { get; set; }
        = new List<PurchaseOrderItem>();
}