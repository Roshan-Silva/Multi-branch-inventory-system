using MultiBranchInventory.Application.Suppliers.DTOs;

namespace MultiBranchInventory.Application.Suppliers.Interfaces;

public interface ISupplierService
{
    Task<IReadOnlyList<SupplierResponse>> GetAllAsync(bool includeInactive, CancellationToken cancellationToken = default);
    Task<SupplierResponse?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<SupplierOperationResult> CreateAsync(CreateSupplierRequest request, CancellationToken cancellationToken = default);
    Task<SupplierOperationResult> UpdateAsync(Guid id, UpdateSupplierRequest request, CancellationToken cancellationToken = default);
    Task<SupplierOperationResult> UpdateStatusAsync(Guid id, UpdateSupplierStatusRequest request, CancellationToken cancellationToken = default);
}
