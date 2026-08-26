using MultiBranchInventory.Domain.Common;
using MultiBranchInventory.Domain.Enums;

namespace MultiBranchInventory.Domain.Entities;

public class PurchaseOrder : BaseEntity
{
    public string OrderNumber { get; set; } = string.Empty;

    public Guid PurchaseRequestId { get; set; }

    public Guid SupplierId { get; set; }

    public Guid BranchId { get; set; }

    public Guid CreatedByUserId { get; set; }

    public Guid? ApprovedByUserId { get; set; }

    public DateTime OrderDate { get; set; } = DateTime.UtcNow;

    public DateTime? ExpectedDeliveryDate { get; set; }

    public PurchaseOrderStatus Status { get; set; }
        = PurchaseOrderStatus.Draft;

    public string? Notes { get; set; }

    public PurchaseRequest PurchaseRequest { get; set; } = null!;

    public Supplier Supplier { get; set; } = null!;

    public Branch Branch { get; set; } = null!;

    public User CreatedByUser { get; set; } = null!;

    public User? ApprovedByUser { get; set; }

    public ICollection<PurchaseOrderItem> Items { get; set; }
        = new List<PurchaseOrderItem>();

    public ICollection<GoodsReceivedNote> GoodsReceivedNotes { get; set; }
        = new List<GoodsReceivedNote>();
}