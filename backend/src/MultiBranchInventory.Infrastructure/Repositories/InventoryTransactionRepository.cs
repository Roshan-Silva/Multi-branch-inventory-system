using Microsoft.EntityFrameworkCore;
using MultiBranchInventory.Application.InventoryTransactions.Interfaces;
using MultiBranchInventory.Domain.Entities;
using MultiBranchInventory.Domain.Enums;
using MultiBranchInventory.Infrastructure.Persistence;

namespace MultiBranchInventory.Infrastructure.Repositories;

public class InventoryTransactionRepository : IInventoryTransactionRepository
{
    private readonly AppDbContext _context;

    public InventoryTransactionRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<InventoryTransaction>> GetAllAsync(
        Guid? inventoryId,
        Guid? branchId,
        Guid? productId,
        InventoryTransactionType? type,
        DateTime? from,
        DateTime? to,
        CancellationToken cancellationToken = default)
    {
        var query = BaseQuery();

        if (inventoryId.HasValue)
            query = query.Where(item => item.InventoryId == inventoryId.Value);
        if (branchId.HasValue)
            query = query.Where(item => item.Inventory.BranchId == branchId.Value);
        if (productId.HasValue)
            query = query.Where(item => item.Inventory.ProductId == productId.Value);
        if (type.HasValue)
            query = query.Where(item => item.Type == type.Value);
        if (from.HasValue)
            query = query.Where(item => item.CreatedAt >= from.Value);
        if (to.HasValue)
            query = query.Where(item => item.CreatedAt <= to.Value);

        return await query.OrderByDescending(item => item.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<InventoryTransaction?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        return await BaseQuery().FirstOrDefaultAsync(
            item => item.Id == id,
            cancellationToken);
    }

    private IQueryable<InventoryTransaction> BaseQuery()
    {
        return _context.InventoryTransactions
            .Include(item => item.Inventory).ThenInclude(inventory => inventory.Branch)
            .Include(item => item.Inventory).ThenInclude(inventory => inventory.Product)
            .Include(item => item.PerformedByUser)
            .AsNoTracking();
    }
}
