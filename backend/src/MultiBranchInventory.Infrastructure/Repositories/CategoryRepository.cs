using Microsoft.EntityFrameworkCore;
using MultiBranchInventory.Application.Categories.Interfaces;
using MultiBranchInventory.Domain.Entities;
using MultiBranchInventory.Infrastructure.Persistence;

namespace MultiBranchInventory.Infrastructure.Repositories;

public class CategoryRepository : ICategoryRepository
{
    private readonly AppDbContext _context;

    public CategoryRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<Category>> GetAllAsync(
        bool includeInactive,
        CancellationToken cancellationToken = default)
    {
        var query = _context.Categories
            .AsNoTracking()
            .AsQueryable();

        if (!includeInactive)
        {
            query = query.Where(category => category.IsActive);
        }

        return await query
            .OrderBy(category => category.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<Category?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        return await _context.Categories.FirstOrDefaultAsync(
            category => category.Id == id,
            cancellationToken);
    }

    public async Task<bool> NameExistsAsync(
        string name,
        Guid? excludeCategoryId = null,
        CancellationToken cancellationToken = default)
    {
        var normalizedName = name.ToLowerInvariant();

        return await _context.Categories.AnyAsync(
            category =>
                category.Name.ToLower() == normalizedName &&
                (!excludeCategoryId.HasValue ||
                 category.Id != excludeCategoryId.Value),
            cancellationToken);
    }

    public async Task AddAsync(
        Category category,
        CancellationToken cancellationToken = default)
    {
        await _context.Categories.AddAsync(category, cancellationToken);
    }

    public async Task SaveChangesAsync(
        CancellationToken cancellationToken = default)
    {
        await _context.SaveChangesAsync(cancellationToken);
    }
}
