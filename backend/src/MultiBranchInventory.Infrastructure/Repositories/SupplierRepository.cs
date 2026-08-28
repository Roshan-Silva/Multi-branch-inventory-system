using Microsoft.EntityFrameworkCore;
using MultiBranchInventory.Application.Suppliers.Interfaces;
using MultiBranchInventory.Domain.Entities;
using MultiBranchInventory.Infrastructure.Persistence;

namespace MultiBranchInventory.Infrastructure.Repositories;

public class SupplierRepository : ISupplierRepository
{
    private readonly AppDbContext _context;

    public SupplierRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<Supplier>> GetAllAsync(
        bool includeInactive,
        CancellationToken cancellationToken = default)
    {
        var query = _context.Suppliers.AsNoTracking().AsQueryable();

        if (!includeInactive)
        {
            query = query.Where(supplier => supplier.IsActive);
        }

        return await query.OrderBy(supplier => supplier.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<Supplier?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        return await _context.Suppliers.FirstOrDefaultAsync(
            supplier => supplier.Id == id,
            cancellationToken);
    }

    public async Task<bool> CodeExistsAsync(
        string code,
        Guid? excludeSupplierId = null,
        CancellationToken cancellationToken = default)
    {
        var normalizedCode = code.ToLowerInvariant();

        return await _context.Suppliers.AnyAsync(
            supplier => supplier.Code.ToLower() == normalizedCode &&
                (!excludeSupplierId.HasValue || supplier.Id != excludeSupplierId.Value),
            cancellationToken);
    }

    public async Task AddAsync(
        Supplier supplier,
        CancellationToken cancellationToken = default)
    {
        await _context.Suppliers.AddAsync(supplier, cancellationToken);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await _context.SaveChangesAsync(cancellationToken);
    }
}
