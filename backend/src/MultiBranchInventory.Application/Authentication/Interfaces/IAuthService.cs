using MultiBranchInventory.Application.Authentication.DTOs;

namespace MultiBranchInventory.Application.Authentication.Interfaces;

public interface IAuthService
{
    Task<LoginResponse?> LoginAsync(
        LoginRequest request,
        CancellationToken cancellationToken = default);
}