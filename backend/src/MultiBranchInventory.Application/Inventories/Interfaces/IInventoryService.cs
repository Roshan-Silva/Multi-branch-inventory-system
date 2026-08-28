using MultiBranchInventory.Application.Inventories.DTOs;

namespace MultiBranchInventory.Application.Inventories.Interfaces;

public interface IInventoryService
{
    Task<InventoryQueryResult> GetAllAsync(Guid? branchId, Guid? productId, bool lowStockOnly, CancellationToken cancellationToken = default);
    Task<InventoryOperationResult> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<InventoryOperationResult> CreateAsync(CreateInventoryRequest request, CancellationToken cancellationToken = default);
    Task<InventoryOperationResult> UpdateLevelsAsync(Guid id, UpdateInventoryLevelsRequest request, CancellationToken cancellationToken = default);
    Task<InventoryOperationResult> AdjustAsync(Guid id, AdjustInventoryRequest request, CancellationToken cancellationToken = default);
}
