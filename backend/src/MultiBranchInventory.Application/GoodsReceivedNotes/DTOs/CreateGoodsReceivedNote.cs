using System.ComponentModel.DataAnnotations;

namespace MultiBranchInventory.Application.GoodsReceivedNotes.DTOs;

public class CreateGoodsReceivedNote
{
    [Required]
    public Guid PurchaseOrderId { get; set; }
    [MaxLength(100)]
    public string? DeliveryReference { get; set; }
    [MaxLength(1000)]
    public string? Notes { get; set; }
    [Required]
    [MinLength(1)]
    public List<CreateGoodsReceivedItem> Items { get; set; } = new();
}

public class CreateGoodsReceivedItem
{
    [Required]
    public Guid PurchaseOrderItemId { get; set; }
    [Range(1, int.MaxValue)]
    public int ReceivedQuantity { get; set; }
    [MaxLength(500)]
    public string? Notes { get; set; }
}
