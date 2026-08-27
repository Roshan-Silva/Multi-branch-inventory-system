using MultiBranchInventory.Domain.Entities;

namespace MultiBranchInventory.Application.Users.Interfaces;

public interface IUserRepository
{
    Task<IReadOnlyList<User>> GetAllAsync(
        bool includeInactive,
        CancellationToken cancellationToken = default);

    Task<User?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<bool> EmailExistsAsync(
        string email,
        Guid? excludeUserId = null,
        CancellationToken cancellationToken = default);

    Task AddAsync(
        User user,
        CancellationToken cancellationToken = default);

    Task SaveChangesAsync(
        CancellationToken cancellationToken = default);
}
