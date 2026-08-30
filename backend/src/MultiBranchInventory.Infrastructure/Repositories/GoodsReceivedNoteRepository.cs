using Microsoft.EntityFrameworkCore;
using MultiBranchInventory.Application.GoodsReceivedNotes.Interfaces;
using MultiBranchInventory.Domain.Entities;
using MultiBranchInventory.Domain.Enums;
using MultiBranchInventory.Infrastructure.Persistence;

namespace MultiBranchInventory.Infrastructure.Repositories;

public class GoodsReceivedNoteRepository : IGoodsReceivedNoteRepository
{
    private readonly AppDbContext _context;
    public GoodsReceivedNoteRepository(AppDbContext context) { _context = context; }

    public async Task<IReadOnlyList<GoodsReceivedNote>> GetAllAsync(
        Guid? branchId, Guid? purchaseOrderId, GoodsReceivedNoteStatus? status,
        Guid? supplierId, DateTime? from, DateTime? to,
        CancellationToken cancellationToken = default)
    {
        var query = FullQuery().AsNoTrackingWithIdentityResolution();
        if (branchId.HasValue) query = query.Where(note => note.PurchaseOrder.BranchId == branchId.Value);
        if (purchaseOrderId.HasValue) query = query.Where(note => note.PurchaseOrderId == purchaseOrderId.Value);
        if (status.HasValue) query = query.Where(note => note.Status == status.Value);
        if (supplierId.HasValue) query = query.Where(note => note.PurchaseOrder.SupplierId == supplierId.Value);
        if (from.HasValue) query = query.Where(note => note.ReceivedDate >= from.Value);
        if (to.HasValue) query = query.Where(note => note.ReceivedDate <= to.Value);
        return await query.OrderByDescending(note => note.ReceivedDate).ToListAsync(cancellationToken);
    }

    public async Task<GoodsReceivedNote?> GetByIdAsync(
        Guid id, bool asTracking, CancellationToken cancellationToken = default)
    {
        var query = FullQuery();
        if (!asTracking) query = query.AsNoTrackingWithIdentityResolution();
        return await query.FirstOrDefaultAsync(note => note.Id == id, cancellationToken);
    }

    public async Task<PurchaseOrder?> GetPurchaseOrderAsync(
        Guid id, bool asTracking, CancellationToken cancellationToken = default)
    {
        var query = _context.PurchaseOrders
            .Include(order => order.Branch)
            .Include(order => order.Supplier)
            .Include(order => order.Items).ThenInclude(item => item.Product)
            .Include(order => order.GoodsReceivedNotes).ThenInclude(note => note.Items)
            .AsSplitQuery();
        if (!asTracking) query = query.AsNoTrackingWithIdentityResolution();
        return await query.FirstOrDefaultAsync(order => order.Id == id, cancellationToken);
    }

    public async Task<int> GetConfirmedReceivedQuantityAsync(
        Guid purchaseOrderItemId, CancellationToken cancellationToken = default) =>
        await _context.GoodsReceivedItems
            .Where(item => item.PurchaseOrderItemId == purchaseOrderItemId &&
                item.GoodsReceivedNote.Status == GoodsReceivedNoteStatus.Confirmed)
            .SumAsync(item => item.ReceivedQuantity, cancellationToken);

    public Task<Inventory?> GetInventoryAsync(
        Guid branchId, Guid productId, CancellationToken cancellationToken = default) =>
        _context.Inventories.Include(inventory => inventory.Branch)
            .Include(inventory => inventory.Product)
            .FirstOrDefaultAsync(inventory => inventory.BranchId == branchId &&
                inventory.ProductId == productId, cancellationToken);

    public async Task AddAsync(GoodsReceivedNote note, CancellationToken cancellationToken = default) =>
        await _context.GoodsReceivedNotes.AddAsync(note, cancellationToken);
    public async Task AddInventoryTransactionAsync(InventoryTransaction transaction, CancellationToken cancellationToken = default) =>
        await _context.InventoryTransactions.AddAsync(transaction, cancellationToken);
    public async Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        await _context.SaveChangesAsync(cancellationToken);

    private IQueryable<GoodsReceivedNote> FullQuery() =>
        _context.GoodsReceivedNotes
            .Include(note => note.PurchaseOrder).ThenInclude(order => order.Branch)
            .Include(note => note.PurchaseOrder).ThenInclude(order => order.Supplier)
            .Include(note => note.PurchaseOrder).ThenInclude(order => order.Items).ThenInclude(item => item.Product)
            .Include(note => note.ReceivedByUser)
            .Include(note => note.ConfirmedByUser)
            .Include(note => note.Items).ThenInclude(item => item.PurchaseOrderItem).ThenInclude(item => item.Product)
            .Include(note => note.Items).ThenInclude(item => item.PurchaseOrderItem)
                .ThenInclude(item => item.GoodsReceivedItems).ThenInclude(item => item.GoodsReceivedNote)
            .AsSplitQuery();
}
