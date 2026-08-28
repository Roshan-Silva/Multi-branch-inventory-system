using MultiBranchInventory.Domain.Entities;

namespace MultiBranchInventory.Application.Suppliers.Interfaces;

public interface ISupplierRepository
{
    Task<IReadOnlyList<Supplier>> GetAllAsync(bool includeInactive, CancellationToken cancellationToken = default);
    Task<Supplier?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<bool> CodeExistsAsync(string code, Guid? excludeSupplierId = null, CancellationToken cancellationToken = default);
    Task AddAsync(Supplier supplier, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
