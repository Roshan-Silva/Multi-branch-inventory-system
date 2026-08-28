using MultiBranchInventory.Application.InventoryTransactions.DTOs;
using MultiBranchInventory.Domain.Enums;

namespace MultiBranchInventory.Application.InventoryTransactions.Interfaces;

public interface IInventoryTransactionService
{
    Task<InventoryTransactionQueryResult> GetAllAsync(
        Guid? inventoryId,
        Guid? branchId,
        Guid? productId,
        InventoryTransactionType? type,
        DateTime? from,
        DateTime? to,
        CancellationToken cancellationToken = default);

    Task<InventoryTransactionOperationResult> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
}
