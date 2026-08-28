using MultiBranchInventory.Domain.Entities;
using MultiBranchInventory.Domain.Enums;

namespace MultiBranchInventory.Application.InventoryTransactions.Interfaces;

public interface IInventoryTransactionRepository
{
    Task<IReadOnlyList<InventoryTransaction>> GetAllAsync(
        Guid? inventoryId,
        Guid? branchId,
        Guid? productId,
        InventoryTransactionType? type,
        DateTime? from,
        DateTime? to,
        CancellationToken cancellationToken = default);

    Task<InventoryTransaction?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
}
