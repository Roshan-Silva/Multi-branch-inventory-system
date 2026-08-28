using MultiBranchInventory.Application.PurchaseOrders.DTOs;
using MultiBranchInventory.Domain.Enums;

namespace MultiBranchInventory.Application.PurchaseOrders.Interfaces;

public interface IPurchaseOrderService
{
    Task<PurchaseOrderQueryResult> GetAllAsync(Guid? branchId, Guid? supplierId, PurchaseOrderStatus? status, Guid? purchaseRequestId, CancellationToken cancellationToken = default);
    Task<PurchaseOrderOperationResult> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PurchaseOrderOperationResult> CreateAsync(CreatePurchaseOrder request, CancellationToken cancellationToken = default);
    Task<PurchaseOrderOperationResult> SubmitAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PurchaseOrderOperationResult> ApproveAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PurchaseOrderOperationResult> SendAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PurchaseOrderOperationResult> ConfirmAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PurchaseOrderOperationResult> CancelAsync(Guid id, CancellationToken cancellationToken = default);
}
