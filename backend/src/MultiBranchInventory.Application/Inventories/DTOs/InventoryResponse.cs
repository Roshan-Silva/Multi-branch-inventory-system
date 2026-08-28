namespace MultiBranchInventory.Application.Inventories.DTOs;

public class InventoryResponse
{
    public Guid Id { get; set; }
    public Guid BranchId { get; set; }
    public string BranchCode { get; set; } = string.Empty;
    public string BranchName { get; set; } = string.Empty;
    public Guid ProductId { get; set; }
    public string ProductSku { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public int QuantityOnHand { get; set; }
    public int MinimumStockLevel { get; set; }
    public int ReorderLevel { get; set; }
    public bool IsLowStock { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
