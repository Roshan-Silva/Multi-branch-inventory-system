using System.ComponentModel.DataAnnotations;

namespace MultiBranchInventory.Application.PurchaseRequests.DTOs;

public class CreatePurchaseRequest
{
    public Guid? BranchId { get; set; }

    [MaxLength(1000)]
    public string? Reason { get; set; }

    [Required]
    [MinLength(1)]
    public List<PurchaseRequestItemRequest> Items { get; set; } = new();
}

public class PurchaseRequestItemRequest
{
    [Required]
    public Guid ProductId { get; set; }

    [Range(1, int.MaxValue)]
    public int RequestedQuantity { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }
}
