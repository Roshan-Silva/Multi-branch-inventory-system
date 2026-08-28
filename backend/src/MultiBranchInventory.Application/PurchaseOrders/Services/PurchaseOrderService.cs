using MultiBranchInventory.Application.Authentication.Interfaces;
using MultiBranchInventory.Application.PurchaseOrders.DTOs;
using MultiBranchInventory.Application.PurchaseOrders.Interfaces;
using MultiBranchInventory.Application.PurchaseRequests.Interfaces;
using MultiBranchInventory.Application.Suppliers.Interfaces;
using MultiBranchInventory.Domain.Entities;
using MultiBranchInventory.Domain.Enums;

namespace MultiBranchInventory.Application.PurchaseOrders.Services;

public class PurchaseOrderService : IPurchaseOrderService
{
    private readonly IPurchaseOrderRepository _repository;
    private readonly IPurchaseRequestRepository _purchaseRequestRepository;
    private readonly ISupplierRepository _supplierRepository;
    private readonly ICurrentUserContext _currentUser;

    public PurchaseOrderService(
        IPurchaseOrderRepository repository,
        IPurchaseRequestRepository purchaseRequestRepository,
        ISupplierRepository supplierRepository,
        ICurrentUserContext currentUser)
    {
        _repository = repository;
        _purchaseRequestRepository = purchaseRequestRepository;
        _supplierRepository = supplierRepository;
        _currentUser = currentUser;
    }

    public async Task<PurchaseOrderQueryResult> GetAllAsync(
        Guid? branchId, Guid? supplierId, PurchaseOrderStatus? status,
        Guid? purchaseRequestId, CancellationToken cancellationToken = default)
    {
        var scope = ResolveReadBranch(branchId);
        if (scope.Failure is not null)
            return PurchaseOrderQueryResult.Failure(scope.Failure.Value.Code, scope.Failure.Value.Message);
        var orders = await _repository.GetAllAsync(
            scope.BranchId, supplierId, status, purchaseRequestId, cancellationToken);
        return PurchaseOrderQueryResult.Success(orders.Select(MapToResponse).ToList());
    }

    public async Task<PurchaseOrderOperationResult> GetByIdAsync(
        Guid id, CancellationToken cancellationToken = default)
    {
        var order = await _repository.GetByIdAsync(id, false, cancellationToken);
        if (order is null) return NotFound();
        if (!CanReadBranch(order.BranchId)) return ForbiddenBranch();
        return PurchaseOrderOperationResult.Success(MapToResponse(order));
    }

    public async Task<PurchaseOrderOperationResult> CreateAsync(
        CreatePurchaseOrder request, CancellationToken cancellationToken = default)
    {
        if (_currentUser.Role is not UserRole.ProcurementOfficer and not UserRole.SuperAdmin)
            return Failure("FORBIDDEN", "You are not allowed to create purchase orders.");
        if (_currentUser.UserId == Guid.Empty)
            return Failure("FORBIDDEN", "Authenticated user information is unavailable.");
        if (request.Items.Count == 0)
            return Failure("INVALID_ITEMS", "At least one item is required.");
        if (request.Items.GroupBy(item => item.PurchaseRequestItemId).Any(group => group.Count() > 1))
            return Failure("DUPLICATE_ITEM", "Duplicate purchase request items are not allowed.");

        var purchaseRequest = await _purchaseRequestRepository.GetByIdAsync(
            request.PurchaseRequestId, true, cancellationToken);
        if (purchaseRequest is null)
            return Failure("PURCHASE_REQUEST_NOT_FOUND", "The selected purchase request was not found.");
        if (purchaseRequest.Status is not PurchaseRequestStatus.Approved and not PurchaseRequestStatus.Converted)
            return InvalidTransition("The purchase request is not approved for ordering.");

        var supplier = await _supplierRepository.GetByIdAsync(request.SupplierId, cancellationToken);
        if (supplier is null) return Failure("SUPPLIER_NOT_FOUND", "The selected supplier was not found.");
        if (!supplier.IsActive) return Failure("SUPPLIER_INACTIVE", "The selected supplier is inactive.");

        var requestItems = purchaseRequest.Items.ToDictionary(item => item.Id);
        var orderItems = new List<PurchaseOrderItem>();
        foreach (var itemRequest in request.Items)
        {
            if (!requestItems.TryGetValue(itemRequest.PurchaseRequestItemId, out var requestItem))
                return Failure("INVALID_ITEM", "The purchase request item does not belong to the selected purchase request.");
            if (itemRequest.OrderedQuantity <= 0)
                return Failure("INVALID_QUANTITY", "Ordered quantity must be greater than zero.");
            if (itemRequest.UnitPrice < 0)
                return Failure("INVALID_PRICE", "Unit price cannot be negative.");

            var allocated = purchaseRequest.PurchaseOrders
                .Where(order => order.Status != PurchaseOrderStatus.Cancelled)
                .SelectMany(order => order.Items)
                .Where(item => item.PurchaseRequestItemId == requestItem.Id)
                .Sum(item => item.OrderedQuantity);
            if (itemRequest.OrderedQuantity > requestItem.RequestedQuantity - allocated)
                return Failure("OVER_ALLOCATED_QUANTITY", "Ordered quantity exceeds the remaining requested quantity.");

            orderItems.Add(new PurchaseOrderItem
            {
                PurchaseRequestItemId = requestItem.Id,
                PurchaseRequestItem = requestItem,
                ProductId = requestItem.ProductId,
                Product = requestItem.Product,
                OrderedQuantity = itemRequest.OrderedQuantity,
                UnitPrice = itemRequest.UnitPrice
            });
        }

        var order = new PurchaseOrder
        {
            OrderNumber = GenerateNumber(),
            PurchaseRequestId = purchaseRequest.Id,
            PurchaseRequest = purchaseRequest,
            SupplierId = supplier.Id,
            Supplier = supplier,
            BranchId = purchaseRequest.BranchId,
            Branch = purchaseRequest.Branch,
            CreatedByUserId = _currentUser.UserId,
            ExpectedDeliveryDate = request.ExpectedDeliveryDate,
            Status = PurchaseOrderStatus.Draft,
            Notes = NormalizeOptional(request.Notes),
            Items = orderItems
        };
        foreach (var item in order.Items) item.PurchaseOrder = order;
        purchaseRequest.PurchaseOrders.Add(order);
        RecalculatePurchaseRequestStatus(purchaseRequest);

        await _repository.AddAsync(order, cancellationToken);
        await _repository.SaveChangesAsync(cancellationToken);
        return await ReloadAsync(order.Id, cancellationToken);
    }

    public Task<PurchaseOrderOperationResult> SubmitAsync(Guid id, CancellationToken cancellationToken = default) =>
        TransitionAsync(id, PurchaseOrderStatus.Draft, PurchaseOrderStatus.Submitted, false, cancellationToken);
    public Task<PurchaseOrderOperationResult> ApproveAsync(Guid id, CancellationToken cancellationToken = default) =>
        TransitionAsync(id, PurchaseOrderStatus.Submitted, PurchaseOrderStatus.Approved, true, cancellationToken);
    public Task<PurchaseOrderOperationResult> SendAsync(Guid id, CancellationToken cancellationToken = default) =>
        TransitionAsync(id, PurchaseOrderStatus.Approved, PurchaseOrderStatus.SentToSupplier, false, cancellationToken);
    public Task<PurchaseOrderOperationResult> ConfirmAsync(Guid id, CancellationToken cancellationToken = default) =>
        TransitionAsync(id, PurchaseOrderStatus.SentToSupplier, PurchaseOrderStatus.Confirmed, false, cancellationToken);

    public async Task<PurchaseOrderOperationResult> CancelAsync(
        Guid id, CancellationToken cancellationToken = default)
    {
        if (_currentUser.Role is not UserRole.ProcurementOfficer and not UserRole.SuperAdmin)
            return Failure("FORBIDDEN", "You are not allowed to cancel purchase orders.");
        var order = await _repository.GetByIdAsync(id, true, cancellationToken);
        if (order is null) return NotFound();
        if (order.Status is not PurchaseOrderStatus.Draft
            and not PurchaseOrderStatus.Submitted
            and not PurchaseOrderStatus.Approved
            and not PurchaseOrderStatus.SentToSupplier)
            return InvalidTransition();

        order.Status = PurchaseOrderStatus.Cancelled;
        order.UpdatedAt = DateTime.UtcNow;
        RecalculatePurchaseRequestStatus(order.PurchaseRequest);
        await _repository.SaveChangesAsync(cancellationToken);
        return await ReloadAsync(order.Id, cancellationToken);
    }

    private async Task<PurchaseOrderOperationResult> TransitionAsync(
        Guid id, PurchaseOrderStatus from, PurchaseOrderStatus to,
        bool isApproval, CancellationToken cancellationToken)
    {
        if (isApproval && _currentUser.Role != UserRole.SuperAdmin)
            return Failure("FORBIDDEN", "Only a SuperAdmin can approve purchase orders.");
        if (!isApproval && _currentUser.Role is not UserRole.ProcurementOfficer and not UserRole.SuperAdmin)
            return Failure("FORBIDDEN", "You are not allowed to manage purchase orders.");

        var order = await _repository.GetByIdAsync(id, true, cancellationToken);
        if (order is null) return NotFound();
        if (order.Status != from) return InvalidTransition();

        order.Status = to;
        order.UpdatedAt = DateTime.UtcNow;
        if (isApproval) order.ApprovedByUserId = _currentUser.UserId;
        await _repository.SaveChangesAsync(cancellationToken);
        return await ReloadAsync(order.Id, cancellationToken);
    }

    private async Task<PurchaseOrderOperationResult> ReloadAsync(Guid id, CancellationToken cancellationToken)
    {
        var order = await _repository.GetByIdAsync(id, false, cancellationToken);
        return PurchaseOrderOperationResult.Success(MapToResponse(order!));
    }

    private static void RecalculatePurchaseRequestStatus(PurchaseRequest request)
    {
        var fullyAllocated = request.Items.All(requestItem =>
            request.PurchaseOrders
                .Where(order => order.Status != PurchaseOrderStatus.Cancelled)
                .SelectMany(order => order.Items)
                .Where(item => item.PurchaseRequestItemId == requestItem.Id)
                .Sum(item => item.OrderedQuantity) >= requestItem.RequestedQuantity);
        request.Status = fullyAllocated ? PurchaseRequestStatus.Converted : PurchaseRequestStatus.Approved;
        request.UpdatedAt = DateTime.UtcNow;
    }

    private (Guid? BranchId, (string Code, string Message)? Failure) ResolveReadBranch(Guid? requested)
    {
        if (_currentUser.Role is UserRole.SuperAdmin or UserRole.ProcurementOfficer) return (requested, null);
        if (_currentUser.Role is UserRole.BranchManager or UserRole.InventoryOfficer)
        {
            if (!_currentUser.BranchId.HasValue) return (null, ("FORBIDDEN_BRANCH", "Your account is not assigned to a branch."));
            if (requested.HasValue && requested != _currentUser.BranchId) return (null, ("FORBIDDEN_BRANCH", "You cannot access purchase orders belonging to another branch."));
            return (_currentUser.BranchId, null);
        }
        return (null, ("FORBIDDEN", "You are not allowed to access purchase orders."));
    }

    private bool CanReadBranch(Guid branchId) =>
        _currentUser.Role is UserRole.SuperAdmin or UserRole.ProcurementOfficer ||
        (_currentUser.Role is UserRole.BranchManager or UserRole.InventoryOfficer && _currentUser.BranchId == branchId);

    private static PurchaseOrderResponse MapToResponse(PurchaseOrder order)
    {
        var items = order.Items.Select(item => new PurchaseOrderItemResponse
        {
            Id = item.Id, PurchaseRequestItemId = item.PurchaseRequestItemId,
            ProductId = item.ProductId, ProductSku = item.Product.Sku, ProductName = item.Product.Name,
            OrderedQuantity = item.OrderedQuantity, UnitPrice = item.UnitPrice,
            LineTotal = item.OrderedQuantity * item.UnitPrice
        }).ToList();
        return new PurchaseOrderResponse
        {
            Id = order.Id, OrderNumber = order.OrderNumber,
            PurchaseRequestId = order.PurchaseRequestId, PurchaseRequestNumber = order.PurchaseRequest.RequestNumber,
            SupplierId = order.SupplierId, SupplierCode = order.Supplier.Code, SupplierName = order.Supplier.Name,
            BranchId = order.BranchId, BranchCode = order.Branch.Code, BranchName = order.Branch.Name,
            CreatedByUserId = order.CreatedByUserId,
            CreatedByName = $"{order.CreatedByUser.FirstName} {order.CreatedByUser.LastName}".Trim(),
            ApprovedByUserId = order.ApprovedByUserId,
            ApprovedByName = order.ApprovedByUser is null ? null : $"{order.ApprovedByUser.FirstName} {order.ApprovedByUser.LastName}".Trim(),
            OrderDate = order.OrderDate, ExpectedDeliveryDate = order.ExpectedDeliveryDate,
            Status = order.Status, Notes = order.Notes, Items = items,
            TotalAmount = items.Sum(item => item.LineTotal), CreatedAt = order.CreatedAt, UpdatedAt = order.UpdatedAt
        };
    }

    private static string GenerateNumber() => $"PO-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..12].ToUpperInvariant()}";
    private static string? NormalizeOptional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static PurchaseOrderOperationResult Failure(string code, string message) => PurchaseOrderOperationResult.Failure(code, message);
    private static PurchaseOrderOperationResult NotFound() => Failure("NOT_FOUND", "Purchase order was not found.");
    private static PurchaseOrderOperationResult ForbiddenBranch() => Failure("FORBIDDEN_BRANCH", "You cannot access purchase orders belonging to another branch.");
    private static PurchaseOrderOperationResult InvalidTransition(string message = "The purchase order is not in a valid state for this operation.") => Failure("INVALID_TRANSITION", message);
}
