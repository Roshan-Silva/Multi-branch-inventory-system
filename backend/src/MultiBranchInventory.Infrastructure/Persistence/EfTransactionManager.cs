using System.Data;
using Microsoft.EntityFrameworkCore;
using MultiBranchInventory.Application.Common.Interfaces;

namespace MultiBranchInventory.Infrastructure.Persistence;

public class EfTransactionManager : ITransactionManager
{
    private readonly AppDbContext _context;
    public EfTransactionManager(AppDbContext context) { _context = context; }

    public async Task<IApplicationTransaction> BeginSerializableAsync(
        CancellationToken cancellationToken = default)
    {
        var transaction = await _context.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);
        return new EfApplicationTransaction(transaction);
    }
}
