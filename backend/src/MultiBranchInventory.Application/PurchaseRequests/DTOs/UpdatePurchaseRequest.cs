using System.ComponentModel.DataAnnotations;

namespace MultiBranchInventory.Application.PurchaseRequests.DTOs;

public class UpdatePurchaseRequest
{
    [MaxLength(1000)]
    public string? Reason { get; set; }

    [Required]
    [MinLength(1)]
    public List<PurchaseRequestItemRequest> Items { get; set; } = new();
}
