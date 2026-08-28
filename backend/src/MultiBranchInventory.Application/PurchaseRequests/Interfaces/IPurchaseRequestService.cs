using MultiBranchInventory.Application.PurchaseRequests.DTOs;
using MultiBranchInventory.Domain.Enums;

namespace MultiBranchInventory.Application.PurchaseRequests.Interfaces;

public interface IPurchaseRequestService
{
    Task<PurchaseRequestQueryResult> GetAllAsync(Guid? branchId, PurchaseRequestStatus? status, Guid? productId, CancellationToken cancellationToken = default);
    Task<PurchaseRequestOperationResult> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PurchaseRequestOperationResult> CreateAsync(CreatePurchaseRequest request, CancellationToken cancellationToken = default);
    Task<PurchaseRequestOperationResult> UpdateAsync(Guid id, UpdatePurchaseRequest request, CancellationToken cancellationToken = default);
    Task<PurchaseRequestOperationResult> SubmitAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PurchaseRequestOperationResult> ApproveAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PurchaseRequestOperationResult> RejectAsync(Guid id, RejectPurchaseRequest request, CancellationToken cancellationToken = default);
    Task<PurchaseRequestOperationResult> CancelAsync(Guid id, CancellationToken cancellationToken = default);
}
