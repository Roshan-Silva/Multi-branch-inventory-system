using MultiBranchInventory.Domain.Common;

namespace MultiBranchInventory.Domain.Entities;

public class GoodsReceivedItem : BaseEntity
{
    public Guid GoodsReceivedNoteId { get; set; }

    public Guid PurchaseOrderItemId { get; set; }

    public int ReceivedQuantity { get; set; }

    public string? Notes { get; set; }

    public GoodsReceivedNote GoodsReceivedNote { get; set; } = null!;

    public PurchaseOrderItem PurchaseOrderItem { get; set; } = null!;
}