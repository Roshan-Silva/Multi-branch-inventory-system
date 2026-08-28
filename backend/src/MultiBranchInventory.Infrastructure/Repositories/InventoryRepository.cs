using Microsoft.EntityFrameworkCore;
using MultiBranchInventory.Application.Inventories.Interfaces;
using MultiBranchInventory.Domain.Entities;
using MultiBranchInventory.Infrastructure.Persistence;

namespace MultiBranchInventory.Infrastructure.Repositories;

public class InventoryRepository : IInventoryRepository
{
    private readonly AppDbContext _context;

    public InventoryRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<Inventory>> GetAllAsync(
        Guid? branchId,
        Guid? productId,
        bool lowStockOnly,
        CancellationToken cancellationToken = default)
    {
        var query = _context.Inventories
            .Include(inventory => inventory.Branch)
            .Include(inventory => inventory.Product)
            .AsNoTracking()
            .AsQueryable();

        if (branchId.HasValue)
        {
            query = query.Where(inventory => inventory.BranchId == branchId.Value);
        }

        if (productId.HasValue)
        {
            query = query.Where(inventory => inventory.ProductId == productId.Value);
        }

        if (lowStockOnly)
        {
            query = query.Where(inventory =>
                inventory.QuantityOnHand <= inventory.ReorderLevel);
        }

        return await query
            .OrderBy(inventory => inventory.Branch.Name)
            .ThenBy(inventory => inventory.Product.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<Inventory?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        return await _context.Inventories
            .Include(inventory => inventory.Branch)
            .Include(inventory => inventory.Product)
            .FirstOrDefaultAsync(inventory => inventory.Id == id, cancellationToken);
    }

    public async Task<bool> ExistsAsync(
        Guid branchId,
        Guid productId,
        CancellationToken cancellationToken = default)
    {
        return await _context.Inventories.AnyAsync(
            inventory => inventory.BranchId == branchId &&
                inventory.ProductId == productId,
            cancellationToken);
    }

    public async Task AddAsync(
        Inventory inventory,
        CancellationToken cancellationToken = default)
    {
        await _context.Inventories.AddAsync(inventory, cancellationToken);
    }

    public async Task AddTransactionAsync(
        InventoryTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        await _context.InventoryTransactions.AddAsync(transaction, cancellationToken);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await _context.SaveChangesAsync(cancellationToken);
    }
}
