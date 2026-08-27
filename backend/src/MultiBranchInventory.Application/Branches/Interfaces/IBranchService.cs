using MultiBranchInventory.Application.Branches.DTOs;

namespace MultiBranchInventory.Application.Branches.Interfaces;

public interface IBranchService
{
    Task<IReadOnlyList<BranchResponse>> GetAllAsync(
        bool includeInactive,
        CancellationToken cancellationToken = default);

    Task<BranchResponse?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<BranchOperationResult> CreateAsync(
        CreateBranchRequest request,
        CancellationToken cancellationToken = default);

    Task<BranchOperationResult> UpdateAsync(
        Guid id,
        UpdateBranchRequest request,
        CancellationToken cancellationToken = default);

    Task<BranchOperationResult> UpdateStatusAsync(
        Guid id,
        UpdateBranchStatusRequest request,
        CancellationToken cancellationToken = default);
}