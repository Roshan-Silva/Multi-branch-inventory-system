namespace MultiBranchInventory.Application.PurchaseOrders.DTOs;

public class PurchaseOrderOperationResult
{
    public bool IsSuccess { get; private set; }
    public string? ErrorCode { get; private set; }
    public string? ErrorMessage { get; private set; }
    public PurchaseOrderResponse? PurchaseOrder { get; private set; }
    public static PurchaseOrderOperationResult Success(PurchaseOrderResponse order) =>
        new() { IsSuccess = true, PurchaseOrder = order };
    public static PurchaseOrderOperationResult Failure(string code, string message) =>
        new() { ErrorCode = code, ErrorMessage = message };
}
