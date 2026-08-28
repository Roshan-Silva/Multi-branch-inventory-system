using MultiBranchInventory.Application.Categories.DTOs;
using MultiBranchInventory.Application.Categories.Interfaces;
using MultiBranchInventory.Domain.Entities;

namespace MultiBranchInventory.Application.Categories.Services;

public class CategoryService : ICategoryService
{
    private readonly ICategoryRepository _categoryRepository;

    public CategoryService(ICategoryRepository categoryRepository)
    {
        _categoryRepository = categoryRepository;
    }

    public async Task<IReadOnlyList<CategoryResponse>> GetAllAsync(
        bool includeInactive,
        CancellationToken cancellationToken = default)
    {
        var categories = await _categoryRepository.GetAllAsync(
            includeInactive,
            cancellationToken);

        return categories.Select(MapToResponse).ToList();
    }

    public async Task<CategoryResponse?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var category = await _categoryRepository.GetByIdAsync(
            id,
            cancellationToken);

        return category is null ? null : MapToResponse(category);
    }

    public async Task<CategoryOperationResult> CreateAsync(
        CreateCategoryRequest request,
        CancellationToken cancellationToken = default)
    {
        var normalizedName = request.Name.Trim();

        if (await _categoryRepository.NameExistsAsync(
                normalizedName,
                cancellationToken: cancellationToken))
        {
            return DuplicateNameFailure();
        }

        var category = new Category
        {
            Name = normalizedName,
            Description = NormalizeOptional(request.Description),
            IsActive = true
        };

        await _categoryRepository.AddAsync(category, cancellationToken);
        await _categoryRepository.SaveChangesAsync(cancellationToken);

        return CategoryOperationResult.Success(MapToResponse(category));
    }

    public async Task<CategoryOperationResult> UpdateAsync(
        Guid id,
        UpdateCategoryRequest request,
        CancellationToken cancellationToken = default)
    {
        var category = await _categoryRepository.GetByIdAsync(
            id,
            cancellationToken);

        if (category is null)
        {
            return NotFoundFailure();
        }

        var normalizedName = request.Name.Trim();

        if (await _categoryRepository.NameExistsAsync(
                normalizedName,
                id,
                cancellationToken))
        {
            return DuplicateNameFailure();
        }

        category.Name = normalizedName;
        category.Description = NormalizeOptional(request.Description);
        category.UpdatedAt = DateTime.UtcNow;

        await _categoryRepository.SaveChangesAsync(cancellationToken);

        return CategoryOperationResult.Success(MapToResponse(category));
    }

    public async Task<CategoryOperationResult> UpdateStatusAsync(
        Guid id,
        UpdateCategoryStatusRequest request,
        CancellationToken cancellationToken = default)
    {
        var category = await _categoryRepository.GetByIdAsync(
            id,
            cancellationToken);

        if (category is null)
        {
            return NotFoundFailure();
        }

        category.IsActive = request.IsActive;
        category.UpdatedAt = DateTime.UtcNow;

        await _categoryRepository.SaveChangesAsync(cancellationToken);

        return CategoryOperationResult.Success(MapToResponse(category));
    }

    private static CategoryResponse MapToResponse(Category category)
    {
        return new CategoryResponse
        {
            Id = category.Id,
            Name = category.Name,
            Description = category.Description,
            IsActive = category.IsActive,
            CreatedAt = category.CreatedAt,
            UpdatedAt = category.UpdatedAt
        };
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static CategoryOperationResult DuplicateNameFailure()
    {
        return CategoryOperationResult.Failure(
            "DUPLICATE_NAME",
            "A category with this name already exists.");
    }

    private static CategoryOperationResult NotFoundFailure()
    {
        return CategoryOperationResult.Failure(
            "NOT_FOUND",
            "Category was not found.");
    }
}
