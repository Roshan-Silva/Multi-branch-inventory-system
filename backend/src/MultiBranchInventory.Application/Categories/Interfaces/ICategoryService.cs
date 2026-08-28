using MultiBranchInventory.Application.Categories.DTOs;

namespace MultiBranchInventory.Application.Categories.Interfaces;

public interface ICategoryService
{
    Task<IReadOnlyList<CategoryResponse>> GetAllAsync(
        bool includeInactive,
        CancellationToken cancellationToken = default);

    Task<CategoryResponse?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<CategoryOperationResult> CreateAsync(
        CreateCategoryRequest request,
        CancellationToken cancellationToken = default);

    Task<CategoryOperationResult> UpdateAsync(
        Guid id,
        UpdateCategoryRequest request,
        CancellationToken cancellationToken = default);

    Task<CategoryOperationResult> UpdateStatusAsync(
        Guid id,
        UpdateCategoryStatusRequest request,
        CancellationToken cancellationToken = default);
}
