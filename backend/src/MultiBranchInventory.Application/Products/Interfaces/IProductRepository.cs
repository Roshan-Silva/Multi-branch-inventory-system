using MultiBranchInventory.Domain.Entities;

namespace MultiBranchInventory.Application.Products.Interfaces;

public interface IProductRepository
{
    Task<IReadOnlyList<Product>> GetAllAsync(
        bool includeInactive,
        Guid? categoryId,
        string? search,
        CancellationToken cancellationToken = default);

    Task<Product?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<bool> SkuExistsAsync(
        string sku,
        Guid? excludeProductId = null,
        CancellationToken cancellationToken = default);

    Task AddAsync(Product product, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
