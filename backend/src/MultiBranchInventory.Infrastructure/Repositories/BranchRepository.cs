using Microsoft.EntityFrameworkCore;
using MultiBranchInventory.Application.Branches.Interfaces;
using MultiBranchInventory.Domain.Entities;
using MultiBranchInventory.Infrastructure.Persistence;

namespace MultiBranchInventory.Infrastructure.Repositories;

public class BranchRepository : IBranchRepository
{
    private readonly AppDbContext _context;

    public BranchRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<Branch>> GetAllAsync(
        bool includeInactive,
        CancellationToken cancellationToken = default)
    {
        var query = _context.Branches
            .AsNoTracking()
            .AsQueryable();

        if (!includeInactive)
        {
            query = query.Where(branch => branch.IsActive);
        }

        return await query
            .OrderBy(branch => branch.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<Branch?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        return await _context.Branches
            .FirstOrDefaultAsync(
                branch => branch.Id == id,
                cancellationToken);
    }

    public async Task<bool> CodeExistsAsync(
        string code,
        Guid? excludeBranchId = null,
        CancellationToken cancellationToken = default)
    {
        return await _context.Branches
            .AnyAsync(
                branch =>
                    branch.Code == code &&
                    (!excludeBranchId.HasValue ||
                     branch.Id != excludeBranchId.Value),
                cancellationToken);
    }

    public async Task AddAsync(
        Branch branch,
        CancellationToken cancellationToken = default)
    {
        await _context.Branches.AddAsync(
            branch,
            cancellationToken);
    }

    public async Task SaveChangesAsync(
        CancellationToken cancellationToken = default)
    {
        await _context.SaveChangesAsync(
            cancellationToken);
    }
}