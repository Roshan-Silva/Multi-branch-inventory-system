using MultiBranchInventory.Domain.Entities;
using MultiBranchInventory.Domain.Enums;

namespace MultiBranchInventory.Application.GoodsReceivedNotes.Interfaces;

public interface IGoodsReceivedNoteRepository
{
    Task<IReadOnlyList<GoodsReceivedNote>> GetAllAsync(
        Guid? branchId, Guid? purchaseOrderId, GoodsReceivedNoteStatus? status,
        Guid? supplierId, DateTime? from, DateTime? to,
        CancellationToken cancellationToken = default);
    Task<GoodsReceivedNote?> GetByIdAsync(Guid id, bool asTracking, CancellationToken cancellationToken = default);
    Task<PurchaseOrder?> GetPurchaseOrderAsync(Guid id, bool asTracking, CancellationToken cancellationToken = default);
    Task<int> GetConfirmedReceivedQuantityAsync(Guid purchaseOrderItemId, CancellationToken cancellationToken = default);
    Task<Inventory?> GetInventoryAsync(Guid branchId, Guid productId, CancellationToken cancellationToken = default);
    Task AddAsync(GoodsReceivedNote note, CancellationToken cancellationToken = default);
    Task AddInventoryTransactionAsync(InventoryTransaction transaction, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
