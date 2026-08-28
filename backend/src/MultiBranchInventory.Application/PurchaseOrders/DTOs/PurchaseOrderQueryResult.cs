namespace MultiBranchInventory.Application.PurchaseOrders.DTOs;

public class PurchaseOrderQueryResult
{
    public bool IsSuccess { get; private set; }
    public string? ErrorCode { get; private set; }
    public string? ErrorMessage { get; private set; }
    public IReadOnlyList<PurchaseOrderResponse> PurchaseOrders { get; private set; }
        = Array.Empty<PurchaseOrderResponse>();
    public static PurchaseOrderQueryResult Success(IReadOnlyList<PurchaseOrderResponse> orders) =>
        new() { IsSuccess = true, PurchaseOrders = orders };
    public static PurchaseOrderQueryResult Failure(string code, string message) =>
        new() { ErrorCode = code, ErrorMessage = message };
}
