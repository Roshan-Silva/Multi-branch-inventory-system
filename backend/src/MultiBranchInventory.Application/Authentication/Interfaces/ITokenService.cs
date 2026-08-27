using MultiBranchInventory.Domain.Entities;

namespace MultiBranchInventory.Application.Authentication.Interfaces;

public interface ITokenService
{
    string GenerateAccessToken(User user);

    DateTime GetAccessTokenExpiration();
}