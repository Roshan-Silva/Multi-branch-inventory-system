using Microsoft.EntityFrameworkCore;
using MultiBranchInventory.Application.PurchaseRequests.Interfaces;
using MultiBranchInventory.Domain.Entities;
using MultiBranchInventory.Domain.Enums;
using MultiBranchInventory.Infrastructure.Persistence;

namespace MultiBranchInventory.Infrastructure.Repositories;

public class PurchaseRequestRepository : IPurchaseRequestRepository
{
    private readonly AppDbContext _context;
    public PurchaseRequestRepository(AppDbContext context) { _context = context; }

    public async Task<IReadOnlyList<PurchaseRequest>> GetAllAsync(
        Guid? branchId,
        PurchaseRequestStatus? status,
        Guid? productId,
        CancellationToken cancellationToken = default)
    {
        var query = FullQuery().AsNoTracking();
        if (branchId.HasValue) query = query.Where(request => request.BranchId == branchId.Value);
        if (status.HasValue) query = query.Where(request => request.Status == status.Value);
        if (productId.HasValue) query = query.Where(request => request.Items.Any(item => item.ProductId == productId.Value));
        return await query.OrderByDescending(request => request.CreatedAt).ToListAsync(cancellationToken);
    }

    public async Task<PurchaseRequest?> GetByIdAsync(
        Guid id,
        bool asTracking,
        CancellationToken cancellationToken = default)
    {
        var query = FullQuery();
        if (!asTracking) query = query.AsNoTracking();
        return await query.FirstOrDefaultAsync(request => request.Id == id, cancellationToken);
    }

    public async Task AddAsync(PurchaseRequest request, CancellationToken cancellationToken = default) =>
        await _context.PurchaseRequests.AddAsync(request, cancellationToken);

    public void RemoveItems(IEnumerable<PurchaseRequestItem> items) =>
        _context.PurchaseRequestItems.RemoveRange(items);

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        await _context.SaveChangesAsync(cancellationToken);

    private IQueryable<PurchaseRequest> FullQuery() =>
        _context.PurchaseRequests
            .Include(request => request.Branch)
            .Include(request => request.RequestedByUser)
            .Include(request => request.ReviewedByUser)
            .Include(request => request.Items).ThenInclude(item => item.Product)
            .Include(request => request.PurchaseOrders).ThenInclude(order => order.Items)
            .AsSplitQuery();
}
