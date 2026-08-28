namespace MultiBranchInventory.Application.Products.DTOs;

public class ProductOperationResult
{
    public bool IsSuccess { get; private set; }
    public string? ErrorCode { get; private set; }
    public string? ErrorMessage { get; private set; }
    public ProductResponse? Product { get; private set; }

    public static ProductOperationResult Success(ProductResponse product)
    {
        return new ProductOperationResult
        {
            IsSuccess = true,
            Product = product
        };
    }

    public static ProductOperationResult Failure(
        string errorCode,
        string errorMessage)
    {
        return new ProductOperationResult
        {
            ErrorCode = errorCode,
            ErrorMessage = errorMessage
        };
    }
}
