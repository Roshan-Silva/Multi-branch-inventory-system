using MultiBranchInventory.Application.Authentication.Interfaces;
using MultiBranchInventory.Application.Common.Interfaces;
using MultiBranchInventory.Application.GoodsReceivedNotes.DTOs;
using MultiBranchInventory.Application.GoodsReceivedNotes.Interfaces;
using MultiBranchInventory.Domain.Entities;
using MultiBranchInventory.Domain.Enums;

namespace MultiBranchInventory.Application.GoodsReceivedNotes.Services;

public class GoodsReceivedNoteService : IGoodsReceivedNoteService
{
    private readonly IGoodsReceivedNoteRepository _repository;
    private readonly ITransactionManager _transactionManager;
    private readonly ICurrentUserContext _currentUser;

    public GoodsReceivedNoteService(
        IGoodsReceivedNoteRepository repository,
        ITransactionManager transactionManager,
        ICurrentUserContext currentUser)
    {
        _repository = repository;
        _transactionManager = transactionManager;
        _currentUser = currentUser;
    }

    public async Task<GoodsReceivedNoteQueryResult> GetAllAsync(
        Guid? branchId, Guid? purchaseOrderId, GoodsReceivedNoteStatus? status,
        Guid? supplierId, DateTime? from, DateTime? to,
        CancellationToken cancellationToken = default)
    {
        var scope = ResolveReadBranch(branchId);
        if (scope.Failure is not null)
            return GoodsReceivedNoteQueryResult.Failure(scope.Failure.Value.Code, scope.Failure.Value.Message);
        var notes = await _repository.GetAllAsync(
            scope.BranchId, purchaseOrderId, status, supplierId, from, to, cancellationToken);
        return GoodsReceivedNoteQueryResult.Success(notes.Select(MapToResponse).ToList());
    }

    public async Task<GoodsReceivedNoteOperationResult> GetByIdAsync(
        Guid id, CancellationToken cancellationToken = default)
    {
        var note = await _repository.GetByIdAsync(id, false, cancellationToken);
        if (note is null) return NotFound();
        if (!CanReadBranch(note.PurchaseOrder.BranchId)) return ForbiddenBranch();
        return GoodsReceivedNoteOperationResult.Success(MapToResponse(note));
    }

    public async Task<GoodsReceivedNoteOperationResult> CreateAsync(
        CreateGoodsReceivedNote request, CancellationToken cancellationToken = default)
    {
        if (!CanModifyRole()) return Forbidden();
        if (_currentUser.UserId == Guid.Empty)
            return Failure("FORBIDDEN", "Authenticated user information is unavailable.");
        if (request.Items.Count == 0)
            return Failure("INVALID_ITEMS", "At least one item is required.");
        if (request.Items.GroupBy(item => item.PurchaseOrderItemId).Any(group => group.Count() > 1))
            return Failure("DUPLICATE_ITEM", "Duplicate purchase order items are not allowed.");

        var order = await _repository.GetPurchaseOrderAsync(
            request.PurchaseOrderId, true, cancellationToken);
        if (order is null)
            return Failure("PURCHASE_ORDER_NOT_FOUND", "The selected purchase order was not found.");
        if (!CanModifyBranch(order.BranchId)) return ForbiddenBranch();
        if (order.Status is not PurchaseOrderStatus.Confirmed and not PurchaseOrderStatus.PartiallyReceived)
            return Failure("INVALID_PO_STATUS", "The purchase order is not in a receivable state.");

        var orderItems = order.Items.ToDictionary(item => item.Id);
        var items = new List<GoodsReceivedItem>();
        foreach (var itemRequest in request.Items)
        {
            if (!orderItems.TryGetValue(itemRequest.PurchaseOrderItemId, out var orderItem))
                return Failure("PURCHASE_ORDER_ITEM_NOT_FOUND", "The purchase order item does not belong to the selected purchase order.");
            if (itemRequest.ReceivedQuantity <= 0)
                return Failure("INVALID_QUANTITY", "Received quantity must be greater than zero.");
            var confirmed = await _repository.GetConfirmedReceivedQuantityAsync(orderItem.Id, cancellationToken);
            if (itemRequest.ReceivedQuantity > orderItem.OrderedQuantity - confirmed)
                return OverReceived();
            items.Add(new GoodsReceivedItem
            {
                PurchaseOrderItemId = orderItem.Id,
                PurchaseOrderItem = orderItem,
                ReceivedQuantity = itemRequest.ReceivedQuantity,
                Notes = NormalizeOptional(itemRequest.Notes)
            });
        }

        var note = new GoodsReceivedNote
        {
            GrnNumber = GenerateNumber(),
            PurchaseOrderId = order.Id,
            PurchaseOrder = order,
            ReceivedByUserId = _currentUser.UserId,
            ReceivedDate = DateTime.UtcNow,
            DeliveryReference = NormalizeOptional(request.DeliveryReference),
            Notes = NormalizeOptional(request.Notes),
            Status = GoodsReceivedNoteStatus.Draft,
            Items = items
        };
        foreach (var item in items) item.GoodsReceivedNote = note;

        await _repository.AddAsync(note, cancellationToken);
        await _repository.SaveChangesAsync(cancellationToken);
        return await ReloadAsync(note.Id, cancellationToken);
    }

    public async Task<GoodsReceivedNoteOperationResult> ConfirmAsync(
        Guid id, CancellationToken cancellationToken = default)
    {
        if (!CanModifyRole()) return Forbidden();
        if (_currentUser.UserId == Guid.Empty) return Forbidden();

        await using var transaction = await _transactionManager.BeginSerializableAsync(cancellationToken);
        try
        {
            var note = await _repository.GetByIdAsync(id, true, cancellationToken);
            if (note is null) return await RollbackAsync(transaction, NotFound(), cancellationToken);
            if (!CanModifyBranch(note.PurchaseOrder.BranchId))
                return await RollbackAsync(transaction, ForbiddenBranch(), cancellationToken);
            if (note.Status != GoodsReceivedNoteStatus.Draft)
                return await RollbackAsync(transaction, InvalidStatus(), cancellationToken);

            var confirmedBefore = new Dictionary<Guid, int>();
            foreach (var orderItem in note.PurchaseOrder.Items)
            {
                confirmedBefore[orderItem.Id] = await _repository.GetConfirmedReceivedQuantityAsync(
                    orderItem.Id, cancellationToken);
            }

            var inventoryPlan = new List<(GoodsReceivedItem Item, Inventory Inventory, int Before, int After)>();
            foreach (var item in note.Items)
            {
                if (item.PurchaseOrderItem.PurchaseOrderId != note.PurchaseOrderId)
                    return await RollbackAsync(transaction,
                        Failure("PURCHASE_ORDER_ITEM_NOT_FOUND", "The purchase order item does not belong to the selected purchase order."), cancellationToken);

                var confirmed = confirmedBefore[item.PurchaseOrderItemId];
                if (item.ReceivedQuantity > item.PurchaseOrderItem.OrderedQuantity - confirmed)
                    return await RollbackAsync(transaction, OverReceived(), cancellationToken);

                var inventory = await _repository.GetInventoryAsync(
                    note.PurchaseOrder.BranchId,
                    item.PurchaseOrderItem.ProductId,
                    cancellationToken);
                if (inventory is null)
                    return await RollbackAsync(transaction,
                        Failure("INVENTORY_NOT_CONFIGURED", "Inventory is not configured for this branch and product."), cancellationToken);

                var after = (long)inventory.QuantityOnHand + item.ReceivedQuantity;
                if (after > int.MaxValue)
                    return await RollbackAsync(transaction,
                        Failure("INVALID_QUANTITY", "Receipt exceeds the supported stock quantity."), cancellationToken);
                inventoryPlan.Add((item, inventory, inventory.QuantityOnHand, (int)after));
            }

            var now = DateTime.UtcNow;
            foreach (var plan in inventoryPlan)
            {
                plan.Inventory.QuantityOnHand = plan.After;
                plan.Inventory.UpdatedAt = now;
                await _repository.AddInventoryTransactionAsync(new InventoryTransaction
                {
                    InventoryId = plan.Inventory.Id,
                    Inventory = plan.Inventory,
                    Type = InventoryTransactionType.PurchaseReceipt,
                    Quantity = plan.Item.ReceivedQuantity,
                    QuantityBefore = plan.Before,
                    QuantityAfter = plan.After,
                    ReferenceNumber = note.GrnNumber,
                    Notes = plan.Item.Notes ?? note.Notes,
                    PerformedByUserId = _currentUser.UserId
                }, cancellationToken);
            }

            note.Status = GoodsReceivedNoteStatus.Confirmed;
            note.ConfirmedByUserId = _currentUser.UserId;
            note.ConfirmedAt = now;
            note.UpdatedAt = now;

            var currentQuantities = note.Items.ToDictionary(
                item => item.PurchaseOrderItemId,
                item => item.ReceivedQuantity);
            var totals = note.PurchaseOrder.Items.Select(orderItem => new
            {
                orderItem.OrderedQuantity,
                ReceivedQuantity = confirmedBefore.GetValueOrDefault(orderItem.Id) +
                    currentQuantities.GetValueOrDefault(orderItem.Id)
            }).ToList();
            note.PurchaseOrder.Status = totals.All(total =>
                total.ReceivedQuantity >= total.OrderedQuantity)
                ? PurchaseOrderStatus.Completed
                : totals.Any(total => total.ReceivedQuantity > 0)
                    ? PurchaseOrderStatus.PartiallyReceived
                    : PurchaseOrderStatus.Confirmed;
            note.PurchaseOrder.UpdatedAt = now;

            await _repository.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return await ReloadAsync(note.Id, cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task<GoodsReceivedNoteOperationResult> CancelAsync(
        Guid id, CancellationToken cancellationToken = default)
    {
        if (!CanModifyRole()) return Forbidden();
        var note = await _repository.GetByIdAsync(id, true, cancellationToken);
        if (note is null) return NotFound();
        if (!CanModifyBranch(note.PurchaseOrder.BranchId)) return ForbiddenBranch();
        if (note.Status != GoodsReceivedNoteStatus.Draft) return InvalidStatus();
        note.Status = GoodsReceivedNoteStatus.Cancelled;
        note.UpdatedAt = DateTime.UtcNow;
        await _repository.SaveChangesAsync(cancellationToken);
        return await ReloadAsync(note.Id, cancellationToken);
    }

    private async Task<GoodsReceivedNoteOperationResult> ReloadAsync(Guid id, CancellationToken token)
    {
        var note = await _repository.GetByIdAsync(id, false, token);
        return GoodsReceivedNoteOperationResult.Success(MapToResponse(note!));
    }

    private static async Task<GoodsReceivedNoteOperationResult> RollbackAsync(
        IApplicationTransaction transaction, GoodsReceivedNoteOperationResult result, CancellationToken token)
    {
        await transaction.RollbackAsync(token);
        return result;
    }

    private (Guid? BranchId, (string Code, string Message)? Failure) ResolveReadBranch(Guid? requested)
    {
        if (_currentUser.Role is UserRole.SuperAdmin or UserRole.ProcurementOfficer) return (requested, null);
        if (_currentUser.Role is UserRole.BranchManager or UserRole.InventoryOfficer)
        {
            if (!_currentUser.BranchId.HasValue) return (null, ("FORBIDDEN_BRANCH", "Your account is not assigned to a branch."));
            if (requested.HasValue && requested != _currentUser.BranchId) return (null, ("FORBIDDEN_BRANCH", "You cannot access goods received notes belonging to another branch."));
            return (_currentUser.BranchId, null);
        }
        return (null, ("FORBIDDEN", "You are not allowed to access goods received notes."));
    }

    private bool CanReadBranch(Guid branchId) =>
        _currentUser.Role is UserRole.SuperAdmin or UserRole.ProcurementOfficer ||
        (_currentUser.Role is UserRole.BranchManager or UserRole.InventoryOfficer && _currentUser.BranchId == branchId);
    private bool CanModifyRole() => _currentUser.Role is UserRole.SuperAdmin or UserRole.InventoryOfficer;
    private bool CanModifyBranch(Guid branchId) =>
        _currentUser.Role == UserRole.SuperAdmin ||
        (_currentUser.Role == UserRole.InventoryOfficer && _currentUser.BranchId == branchId);

    private static GoodsReceivedNoteResponse MapToResponse(GoodsReceivedNote note) => new()
    {
        Id = note.Id, GrnNumber = note.GrnNumber,
        PurchaseOrderId = note.PurchaseOrderId, PurchaseOrderNumber = note.PurchaseOrder.OrderNumber,
        BranchId = note.PurchaseOrder.BranchId, BranchCode = note.PurchaseOrder.Branch.Code, BranchName = note.PurchaseOrder.Branch.Name,
        SupplierId = note.PurchaseOrder.SupplierId, SupplierCode = note.PurchaseOrder.Supplier.Code, SupplierName = note.PurchaseOrder.Supplier.Name,
        ReceivedByUserId = note.ReceivedByUserId,
        ReceivedByName = $"{note.ReceivedByUser.FirstName} {note.ReceivedByUser.LastName}".Trim(),
        ReceivedDate = note.ReceivedDate, DeliveryReference = note.DeliveryReference, Notes = note.Notes,
        Status = note.Status, ConfirmedByUserId = note.ConfirmedByUserId,
        ConfirmedByName = note.ConfirmedByUser is null ? null : $"{note.ConfirmedByUser.FirstName} {note.ConfirmedByUser.LastName}".Trim(),
        ConfirmedAt = note.ConfirmedAt,
        Items = note.Items.Select(item =>
        {
            var confirmed = item.PurchaseOrderItem.GoodsReceivedItems
                .Where(received => received.GoodsReceivedNote.Status == GoodsReceivedNoteStatus.Confirmed)
                .Sum(received => received.ReceivedQuantity);
            return new GoodsReceivedItemResponse
            {
                Id = item.Id, PurchaseOrderItemId = item.PurchaseOrderItemId,
                ProductId = item.PurchaseOrderItem.ProductId,
                ProductSku = item.PurchaseOrderItem.Product.Sku,
                ProductName = item.PurchaseOrderItem.Product.Name,
                OrderedQuantity = item.PurchaseOrderItem.OrderedQuantity,
                AlreadyReceivedQuantity = confirmed,
                ReceivedQuantity = item.ReceivedQuantity,
                RemainingQuantity = Math.Max(0, item.PurchaseOrderItem.OrderedQuantity - confirmed),
                Notes = item.Notes
            };
        }).ToList(),
        CreatedAt = note.CreatedAt, UpdatedAt = note.UpdatedAt
    };

    private static string GenerateNumber() => $"GRN-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..12].ToUpperInvariant()}";
    private static string? NormalizeOptional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static GoodsReceivedNoteOperationResult Failure(string code, string message) => GoodsReceivedNoteOperationResult.Failure(code, message);
    private static GoodsReceivedNoteOperationResult NotFound() => Failure("NOT_FOUND", "Goods received note was not found.");
    private static GoodsReceivedNoteOperationResult Forbidden() => Failure("FORBIDDEN", "You are not allowed to manage goods received notes.");
    private static GoodsReceivedNoteOperationResult ForbiddenBranch() => Failure("FORBIDDEN_BRANCH", "You cannot access or modify goods received notes belonging to another branch.");
    private static GoodsReceivedNoteOperationResult InvalidStatus() => Failure("INVALID_STATUS", "The goods received note is not in a valid state for this operation.");
    private static GoodsReceivedNoteOperationResult OverReceived() => Failure("OVER_RECEIVED_QUANTITY", "Received quantity exceeds the remaining ordered quantity.");
}
