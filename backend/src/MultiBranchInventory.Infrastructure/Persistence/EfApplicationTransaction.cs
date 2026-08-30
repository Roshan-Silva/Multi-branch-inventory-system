using Microsoft.EntityFrameworkCore.Storage;
using MultiBranchInventory.Application.Common.Interfaces;

namespace MultiBranchInventory.Infrastructure.Persistence;

public sealed class EfApplicationTransaction : IApplicationTransaction
{
    private readonly IDbContextTransaction _transaction;
    public EfApplicationTransaction(IDbContextTransaction transaction) { _transaction = transaction; }
    public Task CommitAsync(CancellationToken cancellationToken = default) => _transaction.CommitAsync(cancellationToken);
    public Task RollbackAsync(CancellationToken cancellationToken = default) => _transaction.RollbackAsync(cancellationToken);
    public ValueTask DisposeAsync() => _transaction.DisposeAsync();
}
