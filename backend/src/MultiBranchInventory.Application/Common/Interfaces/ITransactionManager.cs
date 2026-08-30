namespace MultiBranchInventory.Application.Common.Interfaces;

public interface ITransactionManager
{
    Task<IApplicationTransaction> BeginSerializableAsync(
        CancellationToken cancellationToken = default);
}
