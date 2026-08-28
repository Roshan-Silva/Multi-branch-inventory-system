using MultiBranchInventory.Application.Suppliers.DTOs;
using MultiBranchInventory.Application.Suppliers.Interfaces;
using MultiBranchInventory.Domain.Entities;

namespace MultiBranchInventory.Application.Suppliers.Services;

public class SupplierService : ISupplierService
{
    private readonly ISupplierRepository _supplierRepository;

    public SupplierService(ISupplierRepository supplierRepository)
    {
        _supplierRepository = supplierRepository;
    }

    public async Task<IReadOnlyList<SupplierResponse>> GetAllAsync(
        bool includeInactive,
        CancellationToken cancellationToken = default)
    {
        var suppliers = await _supplierRepository.GetAllAsync(
            includeInactive,
            cancellationToken);
        return suppliers.Select(MapToResponse).ToList();
    }

    public async Task<SupplierResponse?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var supplier = await _supplierRepository.GetByIdAsync(id, cancellationToken);
        return supplier is null ? null : MapToResponse(supplier);
    }

    public async Task<SupplierOperationResult> CreateAsync(
        CreateSupplierRequest request,
        CancellationToken cancellationToken = default)
    {
        var normalizedCode = request.Code.Trim().ToUpperInvariant();

        if (await _supplierRepository.CodeExistsAsync(
                normalizedCode,
                cancellationToken: cancellationToken))
        {
            return DuplicateCodeFailure();
        }

        var supplier = new Supplier
        {
            Code = normalizedCode,
            Name = request.Name.Trim(),
            ContactPerson = NormalizeOptional(request.ContactPerson),
            Email = NormalizeEmail(request.Email),
            PhoneNumber = NormalizeOptional(request.PhoneNumber),
            Address = NormalizeOptional(request.Address),
            IsActive = true
        };

        await _supplierRepository.AddAsync(supplier, cancellationToken);
        await _supplierRepository.SaveChangesAsync(cancellationToken);
        return SupplierOperationResult.Success(MapToResponse(supplier));
    }

    public async Task<SupplierOperationResult> UpdateAsync(
        Guid id,
        UpdateSupplierRequest request,
        CancellationToken cancellationToken = default)
    {
        var supplier = await _supplierRepository.GetByIdAsync(id, cancellationToken);

        if (supplier is null)
        {
            return NotFoundFailure();
        }

        var normalizedCode = request.Code.Trim().ToUpperInvariant();

        if (await _supplierRepository.CodeExistsAsync(
                normalizedCode,
                id,
                cancellationToken))
        {
            return DuplicateCodeFailure();
        }

        supplier.Code = normalizedCode;
        supplier.Name = request.Name.Trim();
        supplier.ContactPerson = NormalizeOptional(request.ContactPerson);
        supplier.Email = NormalizeEmail(request.Email);
        supplier.PhoneNumber = NormalizeOptional(request.PhoneNumber);
        supplier.Address = NormalizeOptional(request.Address);
        supplier.UpdatedAt = DateTime.UtcNow;

        await _supplierRepository.SaveChangesAsync(cancellationToken);
        return SupplierOperationResult.Success(MapToResponse(supplier));
    }

    public async Task<SupplierOperationResult> UpdateStatusAsync(
        Guid id,
        UpdateSupplierStatusRequest request,
        CancellationToken cancellationToken = default)
    {
        var supplier = await _supplierRepository.GetByIdAsync(id, cancellationToken);

        if (supplier is null)
        {
            return NotFoundFailure();
        }

        supplier.IsActive = request.IsActive;
        supplier.UpdatedAt = DateTime.UtcNow;
        await _supplierRepository.SaveChangesAsync(cancellationToken);
        return SupplierOperationResult.Success(MapToResponse(supplier));
    }

    private static SupplierResponse MapToResponse(Supplier supplier)
    {
        return new SupplierResponse
        {
            Id = supplier.Id,
            Code = supplier.Code,
            Name = supplier.Name,
            ContactPerson = supplier.ContactPerson,
            Email = supplier.Email,
            PhoneNumber = supplier.PhoneNumber,
            Address = supplier.Address,
            IsActive = supplier.IsActive,
            CreatedAt = supplier.CreatedAt,
            UpdatedAt = supplier.UpdatedAt
        };
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? NormalizeEmail(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToLowerInvariant();

    private static SupplierOperationResult NotFoundFailure() =>
        SupplierOperationResult.Failure("NOT_FOUND", "Supplier was not found.");

    private static SupplierOperationResult DuplicateCodeFailure() =>
        SupplierOperationResult.Failure(
            "DUPLICATE_CODE",
            "A supplier with this code already exists.");
}
