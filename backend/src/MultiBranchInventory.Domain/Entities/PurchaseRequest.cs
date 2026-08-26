using MultiBranchInventory.Domain.Common;
using MultiBranchInventory.Domain.Enums;

namespace MultiBranchInventory.Domain.Entities;

public class PurchaseRequest : BaseEntity
{
    public string RequestNumber { get; set; } = string.Empty;

    public Guid BranchId { get; set; }

    public Guid RequestedByUserId { get; set; }

    public DateTime RequestedDate { get; set; } = DateTime.UtcNow;

    public string? Reason { get; set; }

    public PurchaseRequestStatus Status { get; set; } =
        PurchaseRequestStatus.Draft;

    public Guid? ReviewedByUserId { get; set; }

    public DateTime? ReviewedAt { get; set; }

    public string? RejectionReason { get; set; }

    public Branch Branch { get; set; } = null!;

    public User RequestedByUser { get; set; } = null!;

    public User? ReviewedByUser { get; set; }

    public ICollection<PurchaseRequestItem> Items { get; set; }
        = new List<PurchaseRequestItem>();

    public ICollection<PurchaseOrder> PurchaseOrders { get; set; }
        = new List<PurchaseOrder>();
}