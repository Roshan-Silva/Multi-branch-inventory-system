using Microsoft.AspNetCore.Identity;
using MultiBranchInventory.Application.Authentication.Interfaces;
using MultiBranchInventory.Domain.Entities;

namespace MultiBranchInventory.Infrastructure.Authentication;

public class PasswordHasherService : IPasswordHasher
{
    private readonly PasswordHasher<User> _passwordHasher = new();

    public string Hash(string password)
    {
        var temporaryUser = new User();

        return _passwordHasher.HashPassword(
            temporaryUser,
            password);
    }

    public bool Verify(string password, string passwordHash)
    {
        var temporaryUser = new User();

        var result = _passwordHasher.VerifyHashedPassword(
            temporaryUser,
            passwordHash,
            password);

        return result is PasswordVerificationResult.Success
            or PasswordVerificationResult.SuccessRehashNeeded;
    }
}