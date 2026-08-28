namespace MultiBranchInventory.Application.Categories.DTOs;

public class CategoryOperationResult
{
    public bool IsSuccess { get; private set; }

    public string? ErrorCode { get; private set; }

    public string? ErrorMessage { get; private set; }

    public CategoryResponse? Category { get; private set; }

    public static CategoryOperationResult Success(
        CategoryResponse category)
    {
        return new CategoryOperationResult
        {
            IsSuccess = true,
            Category = category
        };
    }

    public static CategoryOperationResult Failure(
        string errorCode,
        string errorMessage)
    {
        return new CategoryOperationResult
        {
            IsSuccess = false,
            ErrorCode = errorCode,
            ErrorMessage = errorMessage
        };
    }
}
