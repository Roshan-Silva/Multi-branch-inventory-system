using MultiBranchInventory.Application.Authentication.Interfaces;
using MultiBranchInventory.Application.Branches.Interfaces;
using MultiBranchInventory.Application.Inventories.DTOs;
using MultiBranchInventory.Application.Inventories.Interfaces;
using MultiBranchInventory.Application.Products.Interfaces;
using MultiBranchInventory.Domain.Entities;
using MultiBranchInventory.Domain.Enums;

namespace MultiBranchInventory.Application.Inventories.Services;

public class InventoryService : IInventoryService
{
    private readonly IInventoryRepository _inventoryRepository;
    private readonly IBranchRepository _branchRepository;
    private readonly IProductRepository _productRepository;
    private readonly ICurrentUserContext _currentUser;

    public InventoryService(
        IInventoryRepository inventoryRepository,
        IBranchRepository branchRepository,
        IProductRepository productRepository,
        ICurrentUserContext currentUser)
    {
        _inventoryRepository = inventoryRepository;
        _branchRepository = branchRepository;
        _productRepository = productRepository;
        _currentUser = currentUser;
    }

    public async Task<InventoryQueryResult> GetAllAsync(
        Guid? branchId,
        Guid? productId,
        bool lowStockOnly,
        CancellationToken cancellationToken = default)
    {
        var scope = ResolveReadBranch(branchId);
        if (scope.Failure is not null)
            return InventoryQueryResult.Failure(scope.Failure.Value.Code, scope.Failure.Value.Message);

        var inventories = await _inventoryRepository.GetAllAsync(
            scope.BranchId,
            productId,
            lowStockOnly,
            cancellationToken);

        return InventoryQueryResult.Success(inventories.Select(MapToResponse).ToList());
    }

    public async Task<InventoryOperationResult> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var inventory = await _inventoryRepository.GetByIdAsync(id, cancellationToken);
        if (inventory is null)
            return Failure("NOT_FOUND", "Inventory was not found.");
        if (!CanReadBranch(inventory.BranchId))
            return ForbiddenBranch();

        return InventoryOperationResult.Success(MapToResponse(inventory));
    }

    public async Task<InventoryOperationResult> CreateAsync(
        CreateInventoryRequest request,
        CancellationToken cancellationToken = default)
    {
        if (_currentUser.Role != UserRole.SuperAdmin)
            return Failure("FORBIDDEN", "You are not allowed to create inventory configurations.");
        if (!StockLevelsAreValid(request.MinimumStockLevel, request.ReorderLevel))
            return InvalidLevels();

        var branch = await _branchRepository.GetByIdAsync(request.BranchId, cancellationToken);
        if (branch is null)
            return Failure("BRANCH_NOT_FOUND", "The selected branch was not found.");
        if (!branch.IsActive)
            return Failure("BRANCH_INACTIVE", "Inventory cannot be created for an inactive branch.");

        var product = await _productRepository.GetByIdAsync(request.ProductId, cancellationToken);
        if (product is null)
            return Failure("PRODUCT_NOT_FOUND", "The selected product was not found.");
        if (!product.IsActive)
            return Failure("PRODUCT_INACTIVE", "Inventory cannot be created for an inactive product.");

        if (await _inventoryRepository.ExistsAsync(
                request.BranchId,
                request.ProductId,
                cancellationToken))
        {
            return Failure(
                "DUPLICATE_INVENTORY",
                "Inventory already exists for this branch and product.");
        }

        var inventory = new Inventory
        {
            BranchId = branch.Id,
            Branch = branch,
            ProductId = product.Id,
            Product = product,
            QuantityOnHand = 0,
            MinimumStockLevel = request.MinimumStockLevel,
            ReorderLevel = request.ReorderLevel
        };

        await _inventoryRepository.AddAsync(inventory, cancellationToken);
        await _inventoryRepository.SaveChangesAsync(cancellationToken);
        return InventoryOperationResult.Success(MapToResponse(inventory));
    }

    public async Task<InventoryOperationResult> UpdateLevelsAsync(
        Guid id,
        UpdateInventoryLevelsRequest request,
        CancellationToken cancellationToken = default)
    {
        var inventory = await _inventoryRepository.GetByIdAsync(id, cancellationToken);
        if (inventory is null)
            return Failure("NOT_FOUND", "Inventory was not found.");
        if (!CanModify(inventory.BranchId))
            return ForbiddenBranch();
        if (!StockLevelsAreValid(request.MinimumStockLevel, request.ReorderLevel))
            return InvalidLevels();

        inventory.MinimumStockLevel = request.MinimumStockLevel;
        inventory.ReorderLevel = request.ReorderLevel;
        inventory.UpdatedAt = DateTime.UtcNow;
        await _inventoryRepository.SaveChangesAsync(cancellationToken);

        return InventoryOperationResult.Success(MapToResponse(inventory));
    }

    public async Task<InventoryOperationResult> AdjustAsync(
        Guid id,
        AdjustInventoryRequest request,
        CancellationToken cancellationToken = default)
    {
        var inventory = await _inventoryRepository.GetByIdAsync(id, cancellationToken);
        if (inventory is null)
            return Failure("NOT_FOUND", "Inventory was not found.");
        if (!CanModify(inventory.BranchId))
            return ForbiddenBranch();
        if (request.Type is not InventoryTransactionType.AdjustmentIncrease
            and not InventoryTransactionType.AdjustmentDecrease)
        {
            return Failure(
                "INVALID_ADJUSTMENT_TYPE",
                "Only inventory adjustment transaction types are allowed.");
        }
        if (request.Quantity <= 0)
            return Failure("INVALID_QUANTITY", "Adjustment quantity must be greater than zero.");

        var before = inventory.QuantityOnHand;
        var afterLong = request.Type == InventoryTransactionType.AdjustmentIncrease
            ? (long)before + request.Quantity
            : (long)before - request.Quantity;

        if (afterLong < 0)
            return Failure("INSUFFICIENT_STOCK", "Adjustment would result in negative stock.");
        if (afterLong > int.MaxValue)
            return Failure("INVALID_QUANTITY", "Adjustment exceeds the supported stock quantity.");
        if (_currentUser.UserId == Guid.Empty)
            return Failure("FORBIDDEN", "Authenticated user information is unavailable.");

        inventory.QuantityOnHand = (int)afterLong;
        inventory.UpdatedAt = DateTime.UtcNow;

        var transaction = new InventoryTransaction
        {
            InventoryId = inventory.Id,
            Inventory = inventory,
            Type = request.Type,
            Quantity = request.Quantity,
            QuantityBefore = before,
            QuantityAfter = inventory.QuantityOnHand,
            ReferenceNumber = NormalizeOptional(request.ReferenceNumber),
            Notes = NormalizeOptional(request.Notes),
            PerformedByUserId = _currentUser.UserId
        };

        await _inventoryRepository.AddTransactionAsync(transaction, cancellationToken);
        await _inventoryRepository.SaveChangesAsync(cancellationToken);

        return InventoryOperationResult.Success(MapToResponse(inventory));
    }

    private (Guid? BranchId, (string Code, string Message)? Failure) ResolveReadBranch(Guid? requestedBranchId)
    {
        if (_currentUser.Role is UserRole.SuperAdmin or UserRole.ProcurementOfficer)
            return (requestedBranchId, null);

        if (_currentUser.Role is UserRole.BranchManager or UserRole.InventoryOfficer)
        {
            if (!_currentUser.BranchId.HasValue)
                return (null, ("FORBIDDEN_BRANCH", "Your account is not assigned to a branch."));
            if (requestedBranchId.HasValue && requestedBranchId != _currentUser.BranchId)
                return (null, ("FORBIDDEN_BRANCH", "You cannot access inventory belonging to another branch."));
            return (_currentUser.BranchId, null);
        }

        return (null, ("FORBIDDEN", "You are not allowed to access inventory."));
    }

    private bool CanReadBranch(Guid branchId) =>
        _currentUser.Role is UserRole.SuperAdmin or UserRole.ProcurementOfficer ||
        (_currentUser.Role is UserRole.BranchManager or UserRole.InventoryOfficer &&
         _currentUser.BranchId.HasValue && _currentUser.BranchId.Value == branchId);

    private bool CanModify(Guid branchId) =>
        _currentUser.Role == UserRole.SuperAdmin ||
        (_currentUser.Role == UserRole.InventoryOfficer &&
         _currentUser.BranchId.HasValue && _currentUser.BranchId.Value == branchId);

    private static bool StockLevelsAreValid(int minimum, int reorder) =>
        minimum >= 0 && reorder >= 0 && reorder >= minimum;

    private static InventoryResponse MapToResponse(Inventory inventory) => new()
    {
        Id = inventory.Id,
        BranchId = inventory.BranchId,
        BranchCode = inventory.Branch.Code,
        BranchName = inventory.Branch.Name,
        ProductId = inventory.ProductId,
        ProductSku = inventory.Product.Sku,
        ProductName = inventory.Product.Name,
        QuantityOnHand = inventory.QuantityOnHand,
        MinimumStockLevel = inventory.MinimumStockLevel,
        ReorderLevel = inventory.ReorderLevel,
        IsLowStock = inventory.QuantityOnHand <= inventory.ReorderLevel,
        CreatedAt = inventory.CreatedAt,
        UpdatedAt = inventory.UpdatedAt
    };

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static InventoryOperationResult Failure(string code, string message) =>
        InventoryOperationResult.Failure(code, message);

    private static InventoryOperationResult ForbiddenBranch() =>
        Failure("FORBIDDEN_BRANCH", "You cannot modify inventory belonging to another branch.");

    private static InventoryOperationResult InvalidLevels() =>
        Failure("INVALID_STOCK_LEVELS", "Reorder level must be greater than or equal to minimum stock level.");
}
