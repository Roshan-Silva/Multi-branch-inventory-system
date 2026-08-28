using MultiBranchInventory.Domain.Entities;
using MultiBranchInventory.Domain.Enums;

namespace MultiBranchInventory.Application.PurchaseRequests.Interfaces;

public interface IPurchaseRequestRepository
{
    Task<IReadOnlyList<PurchaseRequest>> GetAllAsync(
        Guid? branchId,
        PurchaseRequestStatus? status,
        Guid? productId,
        CancellationToken cancellationToken = default);
    Task<PurchaseRequest?> GetByIdAsync(Guid id, bool asTracking, CancellationToken cancellationToken = default);
    Task AddAsync(PurchaseRequest request, CancellationToken cancellationToken = default);
    void RemoveItems(IEnumerable<PurchaseRequestItem> items);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
