using MultiBranchInventory.Domain.Enums;

namespace MultiBranchInventory.Application.PurchaseOrders.DTOs;

public class PurchaseOrderResponse
{
    public Guid Id { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public Guid PurchaseRequestId { get; set; }
    public string PurchaseRequestNumber { get; set; } = string.Empty;
    public Guid SupplierId { get; set; }
    public string SupplierCode { get; set; } = string.Empty;
    public string SupplierName { get; set; } = string.Empty;
    public Guid BranchId { get; set; }
    public string BranchCode { get; set; } = string.Empty;
    public string BranchName { get; set; } = string.Empty;
    public Guid CreatedByUserId { get; set; }
    public string CreatedByName { get; set; } = string.Empty;
    public Guid? ApprovedByUserId { get; set; }
    public string? ApprovedByName { get; set; }
    public DateTime OrderDate { get; set; }
    public DateTime? ExpectedDeliveryDate { get; set; }
    public PurchaseOrderStatus Status { get; set; }
    public string? Notes { get; set; }
    public IReadOnlyList<PurchaseOrderItemResponse> Items { get; set; }
        = Array.Empty<PurchaseOrderItemResponse>();
    public decimal TotalAmount { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class PurchaseOrderItemResponse
{
    public Guid Id { get; set; }
    public Guid PurchaseRequestItemId { get; set; }
    public Guid ProductId { get; set; }
    public string ProductSku { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public int OrderedQuantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal LineTotal { get; set; }
}
