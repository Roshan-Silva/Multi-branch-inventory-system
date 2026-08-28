namespace MultiBranchInventory.Application.Inventories.DTOs;

public class InventoryQueryResult
{
    public bool IsSuccess { get; private set; }
    public string? ErrorCode { get; private set; }
    public string? ErrorMessage { get; private set; }
    public IReadOnlyList<InventoryResponse> Inventories { get; private set; }
        = Array.Empty<InventoryResponse>();

    public static InventoryQueryResult Success(IReadOnlyList<InventoryResponse> inventories) =>
        new() { IsSuccess = true, Inventories = inventories };

    public static InventoryQueryResult Failure(string code, string message) =>
        new() { ErrorCode = code, ErrorMessage = message };
}
