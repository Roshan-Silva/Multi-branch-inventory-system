namespace MultiBranchInventory.Application.Inventories.DTOs;

public class InventoryOperationResult
{
    public bool IsSuccess { get; private set; }
    public string? ErrorCode { get; private set; }
    public string? ErrorMessage { get; private set; }
    public InventoryResponse? Inventory { get; private set; }

    public static InventoryOperationResult Success(InventoryResponse inventory) =>
        new() { IsSuccess = true, Inventory = inventory };

    public static InventoryOperationResult Failure(string code, string message) =>
        new() { ErrorCode = code, ErrorMessage = message };
}
