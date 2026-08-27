namespace MultiBranchInventory.Application.Users.DTOs;

public class UserOperationResult
{
    public bool IsSuccess { get; private set; }

    public string? ErrorCode { get; private set; }

    public string? ErrorMessage { get; private set; }

    public UserResponse? User { get; private set; }

    public static UserOperationResult Success(UserResponse user)
    {
        return new UserOperationResult
        {
            IsSuccess = true,
            User = user
        };
    }

    public static UserOperationResult Failure(
        string errorCode,
        string errorMessage)
    {
        return new UserOperationResult
        {
            IsSuccess = false,
            ErrorCode = errorCode,
            ErrorMessage = errorMessage
        };
    }
}
