using MultiBranchInventory.Application.Authentication.Interfaces;
using MultiBranchInventory.Application.InventoryTransactions.DTOs;
using MultiBranchInventory.Application.InventoryTransactions.Interfaces;
using MultiBranchInventory.Domain.Entities;
using MultiBranchInventory.Domain.Enums;

namespace MultiBranchInventory.Application.InventoryTransactions.Services;

public class InventoryTransactionService : IInventoryTransactionService
{
    private readonly IInventoryTransactionRepository _repository;
    private readonly ICurrentUserContext _currentUser;

    public InventoryTransactionService(
        IInventoryTransactionRepository repository,
        ICurrentUserContext currentUser)
    {
        _repository = repository;
        _currentUser = currentUser;
    }

    public async Task<InventoryTransactionQueryResult> GetAllAsync(
        Guid? inventoryId,
        Guid? branchId,
        Guid? productId,
        InventoryTransactionType? type,
        DateTime? from,
        DateTime? to,
        CancellationToken cancellationToken = default)
    {
        var scope = ResolveReadBranch(branchId);
        if (scope.Failure is not null)
        {
            return InventoryTransactionQueryResult.Failure(
                scope.Failure.Value.Code,
                scope.Failure.Value.Message);
        }

        var transactions = await _repository.GetAllAsync(
            inventoryId,
            scope.BranchId,
            productId,
            type,
            from,
            to,
            cancellationToken);

        return InventoryTransactionQueryResult.Success(
            transactions.Select(MapToResponse).ToList());
    }

    public async Task<InventoryTransactionOperationResult> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var transaction = await _repository.GetByIdAsync(id, cancellationToken);
        if (transaction is null)
        {
            return InventoryTransactionOperationResult.Failure(
                "NOT_FOUND",
                "Inventory transaction was not found.");
        }

        if (!CanReadBranch(transaction.Inventory.BranchId))
        {
            return InventoryTransactionOperationResult.Failure(
                "FORBIDDEN_BRANCH",
                "You cannot access inventory transactions belonging to another branch.");
        }

        return InventoryTransactionOperationResult.Success(MapToResponse(transaction));
    }

    private (Guid? BranchId, (string Code, string Message)? Failure)
        ResolveReadBranch(Guid? requestedBranchId)
    {
        if (_currentUser.Role is UserRole.SuperAdmin or UserRole.ProcurementOfficer)
            return (requestedBranchId, null);

        if (_currentUser.Role is UserRole.BranchManager or UserRole.InventoryOfficer)
        {
            if (!_currentUser.BranchId.HasValue)
                return (null, ("FORBIDDEN_BRANCH", "Your account is not assigned to a branch."));
            if (requestedBranchId.HasValue && requestedBranchId != _currentUser.BranchId)
                return (null, ("FORBIDDEN_BRANCH", "You cannot access inventory transactions belonging to another branch."));
            return (_currentUser.BranchId, null);
        }

        return (null, ("FORBIDDEN", "You are not allowed to access inventory transactions."));
    }

    private bool CanReadBranch(Guid branchId) =>
        _currentUser.Role is UserRole.SuperAdmin or UserRole.ProcurementOfficer ||
        (_currentUser.Role is UserRole.BranchManager or UserRole.InventoryOfficer &&
         _currentUser.BranchId.HasValue && _currentUser.BranchId.Value == branchId);

    private static InventoryTransactionResponse MapToResponse(
        InventoryTransaction transaction) => new()
    {
        Id = transaction.Id,
        InventoryId = transaction.InventoryId,
        BranchId = transaction.Inventory.BranchId,
        BranchCode = transaction.Inventory.Branch.Code,
        BranchName = transaction.Inventory.Branch.Name,
        ProductId = transaction.Inventory.ProductId,
        ProductSku = transaction.Inventory.Product.Sku,
        ProductName = transaction.Inventory.Product.Name,
        Type = transaction.Type,
        Quantity = transaction.Quantity,
        QuantityBefore = transaction.QuantityBefore,
        QuantityAfter = transaction.QuantityAfter,
        ReferenceNumber = transaction.ReferenceNumber,
        Notes = transaction.Notes,
        PerformedByUserId = transaction.PerformedByUserId,
        PerformedByName = $"{transaction.PerformedByUser.FirstName} {transaction.PerformedByUser.LastName}".Trim(),
        CreatedAt = transaction.CreatedAt
    };
}
