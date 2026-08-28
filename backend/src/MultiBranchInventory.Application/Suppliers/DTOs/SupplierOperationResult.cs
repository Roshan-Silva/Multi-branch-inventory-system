namespace MultiBranchInventory.Application.Suppliers.DTOs;

public class SupplierOperationResult
{
    public bool IsSuccess { get; private set; }
    public string? ErrorCode { get; private set; }
    public string? ErrorMessage { get; private set; }
    public SupplierResponse? Supplier { get; private set; }

    public static SupplierOperationResult Success(SupplierResponse supplier)
    {
        return new SupplierOperationResult
        {
            IsSuccess = true,
            Supplier = supplier
        };
    }

    public static SupplierOperationResult Failure(
        string errorCode,
        string errorMessage)
    {
        return new SupplierOperationResult
        {
            ErrorCode = errorCode,
            ErrorMessage = errorMessage
        };
    }
}
