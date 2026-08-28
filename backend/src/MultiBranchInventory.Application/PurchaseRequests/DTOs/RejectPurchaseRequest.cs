using System.ComponentModel.DataAnnotations;

namespace MultiBranchInventory.Application.PurchaseRequests.DTOs;

public class RejectPurchaseRequest
{
    [Required]
    [MaxLength(1000)]
    public string Reason { get; set; } = string.Empty;
}
