using MultiBranchInventory.Domain.Common;

namespace MultiBranchInventory.Domain.Entities;

public class PurchaseOrderItem : BaseEntity
{
    public Guid PurchaseOrderId { get; set; }

    public Guid PurchaseRequestItemId { get; set; }

    public Guid ProductId { get; set; }

    public int OrderedQuantity { get; set; }

    public decimal UnitPrice { get; set; }

    public PurchaseOrder PurchaseOrder { get; set; } = null!;

    public PurchaseRequestItem PurchaseRequestItem { get; set; } = null!;

    public Product Product { get; set; } = null!;

    public ICollection<GoodsReceivedItem> GoodsReceivedItems { get; set; }
        = new List<GoodsReceivedItem>();
}