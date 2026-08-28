using Microsoft.EntityFrameworkCore;
using MultiBranchInventory.Application.PurchaseOrders.Interfaces;
using MultiBranchInventory.Domain.Entities;
using MultiBranchInventory.Domain.Enums;
using MultiBranchInventory.Infrastructure.Persistence;

namespace MultiBranchInventory.Infrastructure.Repositories;

public class PurchaseOrderRepository : IPurchaseOrderRepository
{
    private readonly AppDbContext _context;
    public PurchaseOrderRepository(AppDbContext context) { _context = context; }

    public async Task<IReadOnlyList<PurchaseOrder>> GetAllAsync(
        Guid? branchId, Guid? supplierId, PurchaseOrderStatus? status,
        Guid? purchaseRequestId, CancellationToken cancellationToken = default)
    {
        var query = FullQuery().AsNoTrackingWithIdentityResolution();
        if (branchId.HasValue) query = query.Where(order => order.BranchId == branchId.Value);
        if (supplierId.HasValue) query = query.Where(order => order.SupplierId == supplierId.Value);
        if (status.HasValue) query = query.Where(order => order.Status == status.Value);
        if (purchaseRequestId.HasValue) query = query.Where(order => order.PurchaseRequestId == purchaseRequestId.Value);
        return await query.OrderByDescending(order => order.CreatedAt).ToListAsync(cancellationToken);
    }

    public async Task<PurchaseOrder?> GetByIdAsync(
        Guid id, bool asTracking, CancellationToken cancellationToken = default)
    {
        var query = FullQuery();
        if (!asTracking)
        {
            query = query.AsNoTrackingWithIdentityResolution();
        }
        return await query.FirstOrDefaultAsync(order => order.Id == id, cancellationToken);
    }

    public async Task AddAsync(PurchaseOrder order, CancellationToken cancellationToken = default) =>
        await _context.PurchaseOrders.AddAsync(order, cancellationToken);
    public async Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        await _context.SaveChangesAsync(cancellationToken);

    private IQueryable<PurchaseOrder> FullQuery() =>
        _context.PurchaseOrders
            .Include(order => order.PurchaseRequest).ThenInclude(request => request.Items)
            .Include(order => order.PurchaseRequest).ThenInclude(request => request.PurchaseOrders).ThenInclude(po => po.Items)
            .Include(order => order.Supplier)
            .Include(order => order.Branch)
            .Include(order => order.CreatedByUser)
            .Include(order => order.ApprovedByUser)
            .Include(order => order.Items).ThenInclude(item => item.Product)
            .Include(order => order.Items).ThenInclude(item => item.PurchaseRequestItem)
            .AsSplitQuery();
}
