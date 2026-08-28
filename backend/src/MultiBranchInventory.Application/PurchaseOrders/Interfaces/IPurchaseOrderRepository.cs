using MultiBranchInventory.Domain.Entities;
using MultiBranchInventory.Domain.Enums;

namespace MultiBranchInventory.Application.PurchaseOrders.Interfaces;

public interface IPurchaseOrderRepository
{
    Task<IReadOnlyList<PurchaseOrder>> GetAllAsync(Guid? branchId, Guid? supplierId, PurchaseOrderStatus? status, Guid? purchaseRequestId, CancellationToken cancellationToken = default);
    Task<PurchaseOrder?> GetByIdAsync(Guid id, bool asTracking, CancellationToken cancellationToken = default);
    Task AddAsync(PurchaseOrder order, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
