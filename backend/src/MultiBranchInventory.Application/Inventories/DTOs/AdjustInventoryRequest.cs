using System.ComponentModel.DataAnnotations;
using MultiBranchInventory.Domain.Enums;

namespace MultiBranchInventory.Application.Inventories.DTOs;

public class AdjustInventoryRequest
{
    public InventoryTransactionType Type { get; set; }

    [Range(1, int.MaxValue)]
    public int Quantity { get; set; }

    [MaxLength(100)]
    public string? ReferenceNumber { get; set; }

    [MaxLength(1000)]
    public string? Notes { get; set; }
}
