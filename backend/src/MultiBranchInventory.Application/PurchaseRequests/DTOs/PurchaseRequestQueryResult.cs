namespace MultiBranchInventory.Application.PurchaseRequests.DTOs;

public class PurchaseRequestQueryResult
{
    public bool IsSuccess { get; private set; }
    public string? ErrorCode { get; private set; }
    public string? ErrorMessage { get; private set; }
    public IReadOnlyList<PurchaseRequestResponse> PurchaseRequests { get; private set; }
        = Array.Empty<PurchaseRequestResponse>();

    public static PurchaseRequestQueryResult Success(IReadOnlyList<PurchaseRequestResponse> requests) =>
        new() { IsSuccess = true, PurchaseRequests = requests };
    public static PurchaseRequestQueryResult Failure(string code, string message) =>
        new() { ErrorCode = code, ErrorMessage = message };
}
