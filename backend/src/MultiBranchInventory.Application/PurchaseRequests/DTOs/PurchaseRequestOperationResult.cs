namespace MultiBranchInventory.Application.PurchaseRequests.DTOs;

public class PurchaseRequestOperationResult
{
    public bool IsSuccess { get; private set; }
    public string? ErrorCode { get; private set; }
    public string? ErrorMessage { get; private set; }
    public PurchaseRequestResponse? PurchaseRequest { get; private set; }

    public static PurchaseRequestOperationResult Success(PurchaseRequestResponse request) =>
        new() { IsSuccess = true, PurchaseRequest = request };
    public static PurchaseRequestOperationResult Failure(string code, string message) =>
        new() { ErrorCode = code, ErrorMessage = message };
}
