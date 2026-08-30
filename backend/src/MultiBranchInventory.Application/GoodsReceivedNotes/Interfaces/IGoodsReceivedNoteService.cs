using MultiBranchInventory.Application.GoodsReceivedNotes.DTOs;
using MultiBranchInventory.Domain.Enums;

namespace MultiBranchInventory.Application.GoodsReceivedNotes.Interfaces;

public interface IGoodsReceivedNoteService
{
    Task<GoodsReceivedNoteQueryResult> GetAllAsync(
        Guid? branchId, Guid? purchaseOrderId, GoodsReceivedNoteStatus? status,
        Guid? supplierId, DateTime? from, DateTime? to,
        CancellationToken cancellationToken = default);
    Task<GoodsReceivedNoteOperationResult> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<GoodsReceivedNoteOperationResult> CreateAsync(CreateGoodsReceivedNote request, CancellationToken cancellationToken = default);
    Task<GoodsReceivedNoteOperationResult> ConfirmAsync(Guid id, CancellationToken cancellationToken = default);
    Task<GoodsReceivedNoteOperationResult> CancelAsync(Guid id, CancellationToken cancellationToken = default);
}
