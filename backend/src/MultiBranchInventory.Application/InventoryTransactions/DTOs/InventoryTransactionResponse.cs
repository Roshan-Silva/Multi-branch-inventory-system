using MultiBranchInventory.Domain.Enums;

namespace MultiBranchInventory.Application.InventoryTransactions.DTOs;

public class InventoryTransactionResponse
{
    public Guid Id { get; set; }
    public Guid InventoryId { get; set; }
    public Guid BranchId { get; set; }
    public string BranchCode { get; set; } = string.Empty;
    public string BranchName { get; set; } = string.Empty;
    public Guid ProductId { get; set; }
    public string ProductSku { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public InventoryTransactionType Type { get; set; }
    public int Quantity { get; set; }
    public int QuantityBefore { get; set; }
    public int QuantityAfter { get; set; }
    public string? ReferenceNumber { get; set; }
    public string? Notes { get; set; }
    public Guid PerformedByUserId { get; set; }
    public string PerformedByName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}
