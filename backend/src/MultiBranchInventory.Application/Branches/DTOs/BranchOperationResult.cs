namespace MultiBranchInventory.Application.Branches.DTOs;

public class BranchOperationResult
{
    public bool IsSuccess { get; private set; }

    public string? ErrorCode { get; private set; }

    public string? ErrorMessage { get; private set; }

    public BranchResponse? Branch { get; private set; }

    public static BranchOperationResult Success(
        BranchResponse branch)
    {
        return new BranchOperationResult
        {
            IsSuccess = true,
            Branch = branch
        };
    }

    public static BranchOperationResult Failure(
        string errorCode,
        string errorMessage)
    {
        return new BranchOperationResult
        {
            IsSuccess = false,
            ErrorCode = errorCode,
            ErrorMessage = errorMessage
        };
    }
}