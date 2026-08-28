using MultiBranchInventory.Application.Categories.Interfaces;
using MultiBranchInventory.Application.Products.DTOs;
using MultiBranchInventory.Application.Products.Interfaces;
using MultiBranchInventory.Domain.Entities;

namespace MultiBranchInventory.Application.Products.Services;

public class ProductService : IProductService
{
    private readonly IProductRepository _productRepository;
    private readonly ICategoryRepository _categoryRepository;

    public ProductService(
        IProductRepository productRepository,
        ICategoryRepository categoryRepository)
    {
        _productRepository = productRepository;
        _categoryRepository = categoryRepository;
    }

    public async Task<IReadOnlyList<ProductResponse>> GetAllAsync(
        bool includeInactive,
        Guid? categoryId,
        string? search,
        CancellationToken cancellationToken = default)
    {
        var products = await _productRepository.GetAllAsync(
            includeInactive,
            categoryId,
            search,
            cancellationToken);

        return products.Select(MapToResponse).ToList();
    }

    public async Task<ProductResponse?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var product = await _productRepository.GetByIdAsync(id, cancellationToken);
        return product is null ? null : MapToResponse(product);
    }

    public async Task<ProductOperationResult> CreateAsync(
        CreateProductRequest request,
        CancellationToken cancellationToken = default)
    {
        var normalizedSku = request.Sku.Trim().ToUpperInvariant();

        if (await _productRepository.SkuExistsAsync(
                normalizedSku,
                cancellationToken: cancellationToken))
        {
            return DuplicateSkuFailure();
        }

        var categoryResult = await GetActiveCategoryAsync(
            request.CategoryId,
            cancellationToken);

        if (categoryResult.Failure is not null)
        {
            return categoryResult.Failure;
        }

        var product = new Product
        {
            Sku = normalizedSku,
            Name = request.Name.Trim(),
            Description = NormalizeOptional(request.Description),
            CategoryId = categoryResult.Category!.Id,
            Category = categoryResult.Category,
            UnitPrice = request.UnitPrice,
            IsActive = true
        };

        await _productRepository.AddAsync(product, cancellationToken);
        await _productRepository.SaveChangesAsync(cancellationToken);

        return ProductOperationResult.Success(MapToResponse(product));
    }

    public async Task<ProductOperationResult> UpdateAsync(
        Guid id,
        UpdateProductRequest request,
        CancellationToken cancellationToken = default)
    {
        var product = await _productRepository.GetByIdAsync(id, cancellationToken);

        if (product is null)
        {
            return NotFoundFailure();
        }

        var normalizedSku = request.Sku.Trim().ToUpperInvariant();

        if (await _productRepository.SkuExistsAsync(
                normalizedSku,
                id,
                cancellationToken))
        {
            return DuplicateSkuFailure();
        }

        var categoryResult = await GetActiveCategoryAsync(
            request.CategoryId,
            cancellationToken);

        if (categoryResult.Failure is not null)
        {
            return categoryResult.Failure;
        }

        product.Sku = normalizedSku;
        product.Name = request.Name.Trim();
        product.Description = NormalizeOptional(request.Description);
        product.CategoryId = categoryResult.Category!.Id;
        product.Category = categoryResult.Category;
        product.UnitPrice = request.UnitPrice;
        product.UpdatedAt = DateTime.UtcNow;

        await _productRepository.SaveChangesAsync(cancellationToken);
        return ProductOperationResult.Success(MapToResponse(product));
    }

    public async Task<ProductOperationResult> UpdateStatusAsync(
        Guid id,
        UpdateProductStatusRequest request,
        CancellationToken cancellationToken = default)
    {
        var product = await _productRepository.GetByIdAsync(id, cancellationToken);

        if (product is null)
        {
            return NotFoundFailure();
        }

        product.IsActive = request.IsActive;
        product.UpdatedAt = DateTime.UtcNow;
        await _productRepository.SaveChangesAsync(cancellationToken);

        return ProductOperationResult.Success(MapToResponse(product));
    }

    private async Task<(Category? Category, ProductOperationResult? Failure)>
        GetActiveCategoryAsync(Guid categoryId, CancellationToken cancellationToken)
    {
        var category = await _categoryRepository.GetByIdAsync(
            categoryId,
            cancellationToken);

        if (category is null)
        {
            return (null, ProductOperationResult.Failure(
                "CATEGORY_NOT_FOUND",
                "The selected category was not found."));
        }

        if (!category.IsActive)
        {
            return (null, ProductOperationResult.Failure(
                "CATEGORY_INACTIVE",
                "Products cannot be assigned to an inactive category."));
        }

        return (category, null);
    }

    private static ProductResponse MapToResponse(Product product)
    {
        return new ProductResponse
        {
            Id = product.Id,
            Sku = product.Sku,
            Name = product.Name,
            Description = product.Description,
            CategoryId = product.CategoryId,
            CategoryName = product.Category.Name,
            UnitPrice = product.UnitPrice,
            IsActive = product.IsActive,
            CreatedAt = product.CreatedAt,
            UpdatedAt = product.UpdatedAt
        };
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static ProductOperationResult NotFoundFailure() =>
        ProductOperationResult.Failure("NOT_FOUND", "Product was not found.");

    private static ProductOperationResult DuplicateSkuFailure() =>
        ProductOperationResult.Failure(
            "DUPLICATE_SKU",
            "A product with this SKU already exists.");
}
