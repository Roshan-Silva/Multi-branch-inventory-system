namespace MultiBranchInventory.Application.InventoryTransactions.DTOs;

public class InventoryTransactionQueryResult
{
    public bool IsSuccess { get; private set; }
    public string? ErrorCode { get; private set; }
    public string? ErrorMessage { get; private set; }
    public IReadOnlyList<InventoryTransactionResponse> Transactions { get; private set; }
        = Array.Empty<InventoryTransactionResponse>();

    public static InventoryTransactionQueryResult Success(IReadOnlyList<InventoryTransactionResponse> transactions) =>
        new() { IsSuccess = true, Transactions = transactions };

    public static InventoryTransactionQueryResult Failure(string code, string message) =>
        new() { ErrorCode = code, ErrorMessage = message };
}
