namespace MultiBranchInventory.Application.Authentication.DTOs;

public class LoginResponse
{
    public string AccessToken { get; set; } = string.Empty;

    public DateTime ExpiresAt { get; set; }

    public AuthUserDto User { get; set; } = null!;
}