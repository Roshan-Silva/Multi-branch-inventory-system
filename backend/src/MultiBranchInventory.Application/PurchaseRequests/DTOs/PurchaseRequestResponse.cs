using MultiBranchInventory.Domain.Enums;

namespace MultiBranchInventory.Application.PurchaseRequests.DTOs;

public class PurchaseRequestResponse
{
    public Guid Id { get; set; }
    public string RequestNumber { get; set; } = string.Empty;
    public Guid BranchId { get; set; }
    public string BranchCode { get; set; } = string.Empty;
    public string BranchName { get; set; } = string.Empty;
    public Guid RequestedByUserId { get; set; }
    public string RequestedByName { get; set; } = string.Empty;
    public DateTime RequestedDate { get; set; }
    public string? Reason { get; set; }
    public PurchaseRequestStatus Status { get; set; }
    public Guid? ReviewedByUserId { get; set; }
    public string? ReviewedByName { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public string? RejectionReason { get; set; }
    public IReadOnlyList<PurchaseRequestItemResponse> Items { get; set; }
        = Array.Empty<PurchaseRequestItemResponse>();
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class PurchaseRequestItemResponse
{
    public Guid Id { get; set; }
    public Guid ProductId { get; set; }
    public string ProductSku { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public int RequestedQuantity { get; set; }
    public string? Notes { get; set; }
}
