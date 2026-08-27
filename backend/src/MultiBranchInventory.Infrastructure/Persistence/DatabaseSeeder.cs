using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MultiBranchInventory.Application.Authentication.Interfaces;
using MultiBranchInventory.Domain.Entities;
using MultiBranchInventory.Domain.Enums;
using MultiBranchInventory.Infrastructure.Authentication;

namespace MultiBranchInventory.Infrastructure.Persistence;

public class DatabaseSeeder
{
    private readonly AppDbContext _context;
    private readonly IPasswordHasher _passwordHasher;
    private readonly AdminSeedSettings _settings;

    public DatabaseSeeder(
        AppDbContext context,
        IPasswordHasher passwordHasher,
        IOptions<AdminSeedSettings> settings)
    {
        _context = context;
        _passwordHasher = passwordHasher;
        _settings = settings.Value;
    }

    public async Task SeedAsync(
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_settings.Email) ||
            string.IsNullOrWhiteSpace(_settings.Password))
        {
            throw new InvalidOperationException(
                "Initial SuperAdmin seed settings are missing.");
        }

        var normalizedEmail = _settings.Email
            .Trim()
            .ToLowerInvariant();

        var adminExists = await _context.Users
            .AnyAsync(
                user => user.Email.ToLower() == normalizedEmail,
                cancellationToken);

        if (adminExists)
        {
            return;
        }

        var admin = new User
        {
            FirstName = _settings.FirstName,
            LastName = _settings.LastName,
            Email = normalizedEmail,
            Role = UserRole.SuperAdmin,
            BranchId = null,
            IsActive = true
        };

        admin.PasswordHash =
            _passwordHasher.Hash(_settings.Password);

        _context.Users.Add(admin);

        await _context.SaveChangesAsync(cancellationToken);
    }
}