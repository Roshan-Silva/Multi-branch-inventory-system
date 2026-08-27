using MultiBranchInventory.Domain.Entities;

namespace MultiBranchInventory.Application.Branches.Interfaces;

public interface IBranchRepository
{
    Task<IReadOnlyList<Branch>> GetAllAsync(
        bool includeInactive,
        CancellationToken cancellationToken = default);

    Task<Branch?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<bool> CodeExistsAsync(
        string code,
        Guid? excludeBranchId = null,
        CancellationToken cancellationToken = default);

    Task AddAsync(
        Branch branch,
        CancellationToken cancellationToken = default);

    Task SaveChangesAsync(
        CancellationToken cancellationToken = default);
}