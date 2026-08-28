using MultiBranchInventory.Domain.Entities;

namespace MultiBranchInventory.Application.Inventories.Interfaces;

public interface IInventoryRepository
{
    Task<IReadOnlyList<Inventory>> GetAllAsync(
        Guid? branchId,
        Guid? productId,
        bool lowStockOnly,
        CancellationToken cancellationToken = default);

    Task<Inventory?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<bool> ExistsAsync(Guid branchId, Guid productId, CancellationToken cancellationToken = default);
    Task AddAsync(Inventory inventory, CancellationToken cancellationToken = default);
    Task AddTransactionAsync(InventoryTransaction transaction, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
