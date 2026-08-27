using MultiBranchInventory.Application.Users.DTOs;

namespace MultiBranchInventory.Application.Users.Interfaces;

public interface IUserService
{
    Task<IReadOnlyList<UserResponse>> GetAllAsync(
        bool includeInactive,
        CancellationToken cancellationToken = default);

    Task<UserResponse?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<UserOperationResult> CreateAsync(
        CreateUserRequest request,
        CancellationToken cancellationToken = default);

    Task<UserOperationResult> UpdateAsync(
        Guid id,
        UpdateUserRequest request,
        CancellationToken cancellationToken = default);

    Task<UserOperationResult> UpdateStatusAsync(
        Guid id,
        UpdateUserStatusRequest request,
        CancellationToken cancellationToken = default);
}
