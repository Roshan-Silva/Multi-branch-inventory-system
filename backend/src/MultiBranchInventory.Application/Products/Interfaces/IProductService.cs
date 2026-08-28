using MultiBranchInventory.Application.Products.DTOs;

namespace MultiBranchInventory.Application.Products.Interfaces;

public interface IProductService
{
    Task<IReadOnlyList<ProductResponse>> GetAllAsync(
        bool includeInactive,
        Guid? categoryId,
        string? search,
        CancellationToken cancellationToken = default);

    Task<ProductResponse?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<ProductOperationResult> CreateAsync(CreateProductRequest request, CancellationToken cancellationToken = default);
    Task<ProductOperationResult> UpdateAsync(Guid id, UpdateProductRequest request, CancellationToken cancellationToken = default);
    Task<ProductOperationResult> UpdateStatusAsync(Guid id, UpdateProductStatusRequest request, CancellationToken cancellationToken = default);
}
