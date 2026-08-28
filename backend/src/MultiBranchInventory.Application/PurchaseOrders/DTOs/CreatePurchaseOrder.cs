using System.ComponentModel.DataAnnotations;

namespace MultiBranchInventory.Application.PurchaseOrders.DTOs;

public class CreatePurchaseOrder
{
    [Required]
    public Guid PurchaseRequestId { get; set; }
    [Required]
    public Guid SupplierId { get; set; }
    public DateTime? ExpectedDeliveryDate { get; set; }
    [MaxLength(1000)]
    public string? Notes { get; set; }
    [Required]
    [MinLength(1)]
    public List<CreatePurchaseOrderItem> Items { get; set; } = new();
}

public class CreatePurchaseOrderItem
{
    [Required]
    public Guid PurchaseRequestItemId { get; set; }
    [Range(1, int.MaxValue)]
    public int OrderedQuantity { get; set; }
    [Range(typeof(decimal), "0", "79228162514264337593543950335")]
    public decimal UnitPrice { get; set; }
}
