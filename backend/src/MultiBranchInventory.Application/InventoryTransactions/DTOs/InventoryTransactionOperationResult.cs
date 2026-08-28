namespace MultiBranchInventory.Application.InventoryTransactions.DTOs;

public class InventoryTransactionOperationResult
{
    public bool IsSuccess { get; private set; }
    public string? ErrorCode { get; private set; }
    public string? ErrorMessage { get; private set; }
    public InventoryTransactionResponse? Transaction { get; private set; }

    public static InventoryTransactionOperationResult Success(InventoryTransactionResponse transaction) =>
        new() { IsSuccess = true, Transaction = transaction };

    public static InventoryTransactionOperationResult Failure(string code, string message) =>
        new() { ErrorCode = code, ErrorMessage = message };
}
