using Microsoft.EntityFrameworkCore;
using MultiBranchInventory.Application.Products.Interfaces;
using MultiBranchInventory.Domain.Entities;
using MultiBranchInventory.Infrastructure.Persistence;

namespace MultiBranchInventory.Infrastructure.Repositories;

public class ProductRepository : IProductRepository
{
    private readonly AppDbContext _context;

    public ProductRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<Product>> GetAllAsync(
        bool includeInactive,
        Guid? categoryId,
        string? search,
        CancellationToken cancellationToken = default)
    {
        var query = _context.Products
            .Include(product => product.Category)
            .AsNoTracking()
            .AsQueryable();

        if (!includeInactive)
        {
            query = query.Where(product => product.IsActive);
        }

        if (categoryId.HasValue)
        {
            query = query.Where(product => product.CategoryId == categoryId.Value);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLowerInvariant();
            query = query.Where(product =>
                product.Sku.ToLower().Contains(term) ||
                product.Name.ToLower().Contains(term));
        }

        return await query
            .OrderBy(product => product.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<Product?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        return await _context.Products
            .Include(product => product.Category)
            .FirstOrDefaultAsync(product => product.Id == id, cancellationToken);
    }

    public async Task<bool> SkuExistsAsync(
        string sku,
        Guid? excludeProductId = null,
        CancellationToken cancellationToken = default)
    {
        var normalizedSku = sku.ToLowerInvariant();

        return await _context.Products.AnyAsync(
            product => product.Sku.ToLower() == normalizedSku &&
                (!excludeProductId.HasValue || product.Id != excludeProductId.Value),
            cancellationToken);
    }

    public async Task AddAsync(
        Product product,
        CancellationToken cancellationToken = default)
    {
        await _context.Products.AddAsync(product, cancellationToken);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await _context.SaveChangesAsync(cancellationToken);
    }
}
