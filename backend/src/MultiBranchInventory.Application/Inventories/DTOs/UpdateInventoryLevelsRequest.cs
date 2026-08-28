using System.ComponentModel.DataAnnotations;

namespace MultiBranchInventory.Application.Inventories.DTOs;

public class UpdateInventoryLevelsRequest
{
    [Range(0, int.MaxValue)]
    public int MinimumStockLevel { get; set; }

    [Range(0, int.MaxValue)]
    public int ReorderLevel { get; set; }
}
