using MultiBranchInventory.Domain.Common;
using MultiBranchInventory.Domain.Enums;

namespace MultiBranchInventory.Domain.Entities;

public class User : BaseEntity
{
    public string FirstName { get; set; } = string.Empty;

    public string LastName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string PasswordHash { get; set; } = string.Empty;

    public UserRole Role { get; set; }

    public Guid? BranchId { get; set; }

    public bool IsActive { get; set; } = true;

    public Branch? Branch { get; set; }

    public ICollection<PurchaseRequest> RequestedPurchaseRequests { get; set; }
        = new List<PurchaseRequest>();

    public ICollection<PurchaseRequest> ReviewedPurchaseRequests { get; set; }
        = new List<PurchaseRequest>();

    public ICollection<PurchaseOrder> CreatedPurchaseOrders { get; set; }
        = new List<PurchaseOrder>();

    public ICollection<PurchaseOrder> ApprovedPurchaseOrders { get; set; }
        = new List<PurchaseOrder>();

    public ICollection<GoodsReceivedNote> ReceivedGoodsNotes { get; set; }
        = new List<GoodsReceivedNote>();

    public ICollection<GoodsReceivedNote> ConfirmedGoodsNotes { get; set; }
        = new List<GoodsReceivedNote>();

    public ICollection<InventoryTransaction> InventoryTransactions { get; set; }
        = new List<InventoryTransaction>();
}