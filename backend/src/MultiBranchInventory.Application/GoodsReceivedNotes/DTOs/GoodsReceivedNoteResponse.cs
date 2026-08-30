using MultiBranchInventory.Domain.Enums;

namespace MultiBranchInventory.Application.GoodsReceivedNotes.DTOs;

public class GoodsReceivedNoteResponse
{
    public Guid Id { get; set; }
    public string GrnNumber { get; set; } = string.Empty;
    public Guid PurchaseOrderId { get; set; }
    public string PurchaseOrderNumber { get; set; } = string.Empty;
    public Guid BranchId { get; set; }
    public string BranchCode { get; set; } = string.Empty;
    public string BranchName { get; set; } = string.Empty;
    public Guid SupplierId { get; set; }
    public string SupplierCode { get; set; } = string.Empty;
    public string SupplierName { get; set; } = string.Empty;
    public Guid ReceivedByUserId { get; set; }
    public string ReceivedByName { get; set; } = string.Empty;
    public DateTime ReceivedDate { get; set; }
    public string? DeliveryReference { get; set; }
    public string? Notes { get; set; }
    public GoodsReceivedNoteStatus Status { get; set; }
    public Guid? ConfirmedByUserId { get; set; }
    public string? ConfirmedByName { get; set; }
    public DateTime? ConfirmedAt { get; set; }
    public IReadOnlyList<GoodsReceivedItemResponse> Items { get; set; }
        = Array.Empty<GoodsReceivedItemResponse>();
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class GoodsReceivedItemResponse
{
    public Guid Id { get; set; }
    public Guid PurchaseOrderItemId { get; set; }
    public Guid ProductId { get; set; }
    public string ProductSku { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public int OrderedQuantity { get; set; }
    public int AlreadyReceivedQuantity { get; set; }
    public int ReceivedQuantity { get; set; }
    public int RemainingQuantity { get; set; }
    public string? Notes { get; set; }
}
