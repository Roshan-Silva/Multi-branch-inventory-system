using MultiBranchInventory.Domain.Common;
using MultiBranchInventory.Domain.Enums;

namespace MultiBranchInventory.Domain.Entities;

public class GoodsReceivedNote : BaseEntity
{
    public string GrnNumber { get; set; } = string.Empty;

    public Guid PurchaseOrderId { get; set; }

    public Guid ReceivedByUserId { get; set; }

    public DateTime ReceivedDate { get; set; } = DateTime.UtcNow;

    public string? DeliveryReference { get; set; }

    public string? Notes { get; set; }

    public PurchaseOrder PurchaseOrder { get; set; } = null!;

    public User ReceivedByUser { get; set; } = null!;

    public ICollection<GoodsReceivedItem> Items { get; set; }
        = new List<GoodsReceivedItem>();

    public GoodsReceivedNoteStatus Status { get; set; }
        = GoodsReceivedNoteStatus.Draft;

    public Guid? ConfirmedByUserId { get; set; }

    public DateTime? ConfirmedAt { get; set; }

    public User? ConfirmedByUser { get; set; }
}