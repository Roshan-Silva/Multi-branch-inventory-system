using MultiBranchInventory.Application.Authentication.Interfaces;
using MultiBranchInventory.Application.Branches.Interfaces;
using MultiBranchInventory.Application.Products.Interfaces;
using MultiBranchInventory.Application.PurchaseRequests.DTOs;
using MultiBranchInventory.Application.PurchaseRequests.Interfaces;
using MultiBranchInventory.Domain.Entities;
using MultiBranchInventory.Domain.Enums;

namespace MultiBranchInventory.Application.PurchaseRequests.Services;

public class PurchaseRequestService : IPurchaseRequestService
{
    private readonly IPurchaseRequestRepository _repository;
    private readonly IBranchRepository _branchRepository;
    private readonly IProductRepository _productRepository;
    private readonly ICurrentUserContext _currentUser;

    public PurchaseRequestService(
        IPurchaseRequestRepository repository,
        IBranchRepository branchRepository,
        IProductRepository productRepository,
        ICurrentUserContext currentUser)
    {
        _repository = repository;
        _branchRepository = branchRepository;
        _productRepository = productRepository;
        _currentUser = currentUser;
    }

    public async Task<PurchaseRequestQueryResult> GetAllAsync(
        Guid? branchId,
        PurchaseRequestStatus? status,
        Guid? productId,
        CancellationToken cancellationToken = default)
    {
        var scope = ResolveReadBranch(branchId);
        if (scope.Failure is not null)
            return PurchaseRequestQueryResult.Failure(scope.Failure.Value.Code, scope.Failure.Value.Message);

        var requests = await _repository.GetAllAsync(
            scope.BranchId, status, productId, cancellationToken);
        return PurchaseRequestQueryResult.Success(requests.Select(MapToResponse).ToList());
    }

    public async Task<PurchaseRequestOperationResult> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var request = await _repository.GetByIdAsync(id, false, cancellationToken);
        if (request is null) return NotFound();
        if (!CanReadBranch(request.BranchId)) return ForbiddenBranch();
        return PurchaseRequestOperationResult.Success(MapToResponse(request));
    }

    public async Task<PurchaseRequestOperationResult> CreateAsync(
        CreatePurchaseRequest request,
        CancellationToken cancellationToken = default)
    {
        var branchResult = await ResolveCreationBranchAsync(request.BranchId, cancellationToken);
        if (branchResult.Failure is not null) return branchResult.Failure;
        var itemsResult = await BuildItemsAsync(request.Items, cancellationToken);
        if (itemsResult.Failure is not null) return itemsResult.Failure;
        if (_currentUser.UserId == Guid.Empty)
            return Failure("FORBIDDEN", "Authenticated user information is unavailable.");

        var entity = new PurchaseRequest
        {
            RequestNumber = GenerateNumber("PR"),
            BranchId = branchResult.Branch!.Id,
            Branch = branchResult.Branch,
            RequestedByUserId = _currentUser.UserId,
            Reason = NormalizeOptional(request.Reason),
            Status = PurchaseRequestStatus.Draft,
            Items = itemsResult.Items!
        };
        foreach (var item in entity.Items) item.PurchaseRequest = entity;

        await _repository.AddAsync(entity, cancellationToken);
        await _repository.SaveChangesAsync(cancellationToken);
        var saved = await _repository.GetByIdAsync(entity.Id, true, cancellationToken);
        return PurchaseRequestOperationResult.Success(MapToResponse(saved!));
    }

    public async Task<PurchaseRequestOperationResult> UpdateAsync(
        Guid id,
        UpdatePurchaseRequest request,
        CancellationToken cancellationToken = default)
    {
        var entity = await _repository.GetByIdAsync(id, true, cancellationToken);
        if (entity is null) return NotFound();
        if (!CanEdit(entity)) return ForbiddenBranch();
        if (entity.Status != PurchaseRequestStatus.Draft) return InvalidTransition();

        var itemsResult = await BuildItemsAsync(request.Items, cancellationToken);
        if (itemsResult.Failure is not null) return itemsResult.Failure;

        var proposedItems = itemsResult.Items!;
        var proposedProductIds = proposedItems
            .Select(item => item.ProductId)
            .ToHashSet();
        var removedItems = entity.Items
            .Where(item => !proposedProductIds.Contains(item.ProductId))
            .ToList();

        _repository.RemoveItems(removedItems);
        foreach (var removedItem in removedItems)
        {
            entity.Items.Remove(removedItem);
        }

        var existingItems = entity.Items
            .ToDictionary(item => item.ProductId);

        foreach (var proposedItem in proposedItems)
        {
            if (existingItems.TryGetValue(
                    proposedItem.ProductId,
                    out var existingItem))
            {
                existingItem.RequestedQuantity =
                    proposedItem.RequestedQuantity;
                existingItem.Notes = proposedItem.Notes;
                existingItem.Product = proposedItem.Product;
                continue;
            }

            proposedItem.PurchaseRequestId = entity.Id;
            proposedItem.PurchaseRequest = entity;
            entity.Items.Add(proposedItem);
        }
        entity.Reason = NormalizeOptional(request.Reason);
        entity.UpdatedAt = DateTime.UtcNow;
        await _repository.SaveChangesAsync(cancellationToken);
        return PurchaseRequestOperationResult.Success(MapToResponse(entity));
    }

    public Task<PurchaseRequestOperationResult> SubmitAsync(Guid id, CancellationToken cancellationToken = default) =>
        TransitionAsync(id, PurchaseRequestStatus.Draft, PurchaseRequestStatus.Submitted, false, cancellationToken);

    public async Task<PurchaseRequestOperationResult> ApproveAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var entity = await _repository.GetByIdAsync(id, true, cancellationToken);
        if (entity is null) return NotFound();
        if (!CanReview(entity.BranchId)) return ForbiddenBranch();
        if (entity.Status != PurchaseRequestStatus.Submitted) return InvalidTransition();

        entity.Status = PurchaseRequestStatus.Approved;
        entity.ReviewedByUserId = _currentUser.UserId;
        entity.ReviewedAt = DateTime.UtcNow;
        entity.RejectionReason = null;
        entity.UpdatedAt = DateTime.UtcNow;
        await _repository.SaveChangesAsync(cancellationToken);
        return PurchaseRequestOperationResult.Success(MapToResponse(entity));
    }

    public async Task<PurchaseRequestOperationResult> RejectAsync(
        Guid id,
        RejectPurchaseRequest request,
        CancellationToken cancellationToken = default)
    {
        var entity = await _repository.GetByIdAsync(id, true, cancellationToken);
        if (entity is null) return NotFound();
        if (!CanReview(entity.BranchId)) return ForbiddenBranch();
        if (entity.Status != PurchaseRequestStatus.Submitted) return InvalidTransition();

        entity.Status = PurchaseRequestStatus.Rejected;
        entity.ReviewedByUserId = _currentUser.UserId;
        entity.ReviewedAt = DateTime.UtcNow;
        entity.RejectionReason = request.Reason.Trim();
        entity.UpdatedAt = DateTime.UtcNow;
        await _repository.SaveChangesAsync(cancellationToken);
        return PurchaseRequestOperationResult.Success(MapToResponse(entity));
    }

    public async Task<PurchaseRequestOperationResult> CancelAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var entity = await _repository.GetByIdAsync(id, true, cancellationToken);
        if (entity is null) return NotFound();
        if (!CanEdit(entity)) return ForbiddenBranch();
        if (entity.Status is not PurchaseRequestStatus.Draft and not PurchaseRequestStatus.Submitted)
            return InvalidTransition();

        entity.Status = PurchaseRequestStatus.Cancelled;
        entity.UpdatedAt = DateTime.UtcNow;
        await _repository.SaveChangesAsync(cancellationToken);
        return PurchaseRequestOperationResult.Success(MapToResponse(entity));
    }

    private async Task<PurchaseRequestOperationResult> TransitionAsync(
        Guid id,
        PurchaseRequestStatus from,
        PurchaseRequestStatus to,
        bool review,
        CancellationToken cancellationToken)
    {
        var entity = await _repository.GetByIdAsync(id, true, cancellationToken);
        if (entity is null) return NotFound();
        if (!CanEdit(entity)) return ForbiddenBranch();
        if (entity.Status != from) return InvalidTransition();
        entity.Status = to;
        entity.UpdatedAt = DateTime.UtcNow;
        await _repository.SaveChangesAsync(cancellationToken);
        return PurchaseRequestOperationResult.Success(MapToResponse(entity));
    }

    private async Task<(Branch? Branch, PurchaseRequestOperationResult? Failure)>
        ResolveCreationBranchAsync(Guid? requestedBranchId, CancellationToken cancellationToken)
    {
        Guid branchId;
        if (_currentUser.Role == UserRole.InventoryOfficer)
        {
            if (!_currentUser.BranchId.HasValue)
                return (null, Failure("FORBIDDEN_BRANCH", "Your account is not assigned to a branch."));
            if (requestedBranchId.HasValue && requestedBranchId != _currentUser.BranchId)
                return (null, ForbiddenBranch());
            branchId = _currentUser.BranchId.Value;
        }
        else if (_currentUser.Role == UserRole.SuperAdmin)
        {
            if (!requestedBranchId.HasValue)
                return (null, Failure("BRANCH_REQUIRED", "A branch is required."));
            branchId = requestedBranchId.Value;
        }
        else return (null, Failure("FORBIDDEN", "You are not allowed to create purchase requests."));

        var branch = await _branchRepository.GetByIdAsync(branchId, cancellationToken);
        if (branch is null) return (null, Failure("BRANCH_NOT_FOUND", "The selected branch was not found."));
        if (!branch.IsActive) return (null, Failure("BRANCH_INACTIVE", "Purchase requests cannot be created for an inactive branch."));
        return (branch, null);
    }

    private async Task<(List<PurchaseRequestItem>? Items, PurchaseRequestOperationResult? Failure)>
        BuildItemsAsync(IReadOnlyCollection<PurchaseRequestItemRequest> requests, CancellationToken cancellationToken)
    {
        if (requests.Count == 0) return (null, Failure("INVALID_ITEMS", "At least one item is required."));
        if (requests.GroupBy(item => item.ProductId).Any(group => group.Count() > 1))
            return (null, Failure("DUPLICATE_ITEM", "Duplicate products are not allowed."));

        var items = new List<PurchaseRequestItem>();
        foreach (var request in requests)
        {
            if (request.RequestedQuantity <= 0)
                return (null, Failure("INVALID_QUANTITY", "Requested quantity must be greater than zero."));
            var product = await _productRepository.GetByIdAsync(request.ProductId, cancellationToken);
            if (product is null) return (null, Failure("PRODUCT_NOT_FOUND", "The selected product was not found."));
            if (!product.IsActive) return (null, Failure("PRODUCT_INACTIVE", "Inactive products cannot be requested."));
            items.Add(new PurchaseRequestItem
            {
                ProductId = product.Id,
                Product = product,
                RequestedQuantity = request.RequestedQuantity,
                Notes = NormalizeOptional(request.Notes)
            });
        }
        return (items, null);
    }

    private (Guid? BranchId, (string Code, string Message)? Failure) ResolveReadBranch(Guid? requested)
    {
        if (_currentUser.Role is UserRole.SuperAdmin or UserRole.ProcurementOfficer) return (requested, null);
        if (_currentUser.Role is UserRole.BranchManager or UserRole.InventoryOfficer)
        {
            if (!_currentUser.BranchId.HasValue) return (null, ("FORBIDDEN_BRANCH", "Your account is not assigned to a branch."));
            if (requested.HasValue && requested != _currentUser.BranchId) return (null, ("FORBIDDEN_BRANCH", "You cannot access purchase requests belonging to another branch."));
            return (_currentUser.BranchId, null);
        }
        return (null, ("FORBIDDEN", "You are not allowed to access purchase requests."));
    }

    private bool CanReadBranch(Guid branchId) =>
        _currentUser.Role is UserRole.SuperAdmin or UserRole.ProcurementOfficer ||
        (_currentUser.Role is UserRole.BranchManager or UserRole.InventoryOfficer && _currentUser.BranchId == branchId);
    private bool CanEdit(PurchaseRequest request) =>
        _currentUser.Role == UserRole.SuperAdmin ||
        (_currentUser.Role == UserRole.InventoryOfficer && _currentUser.BranchId == request.BranchId && _currentUser.UserId == request.RequestedByUserId);
    private bool CanReview(Guid branchId) =>
        _currentUser.Role == UserRole.SuperAdmin ||
        (_currentUser.Role == UserRole.BranchManager && _currentUser.BranchId == branchId);

    private static PurchaseRequestResponse MapToResponse(PurchaseRequest request) => new()
    {
        Id = request.Id, RequestNumber = request.RequestNumber, BranchId = request.BranchId,
        BranchCode = request.Branch.Code, BranchName = request.Branch.Name,
        RequestedByUserId = request.RequestedByUserId,
        RequestedByName = $"{request.RequestedByUser.FirstName} {request.RequestedByUser.LastName}".Trim(),
        RequestedDate = request.RequestedDate, Reason = request.Reason, Status = request.Status,
        ReviewedByUserId = request.ReviewedByUserId,
        ReviewedByName = request.ReviewedByUser is null ? null : $"{request.ReviewedByUser.FirstName} {request.ReviewedByUser.LastName}".Trim(),
        ReviewedAt = request.ReviewedAt, RejectionReason = request.RejectionReason,
        Items = request.Items.Select(item => new PurchaseRequestItemResponse
        {
            Id = item.Id, ProductId = item.ProductId, ProductSku = item.Product.Sku,
            ProductName = item.Product.Name, RequestedQuantity = item.RequestedQuantity, Notes = item.Notes
        }).ToList(),
        CreatedAt = request.CreatedAt, UpdatedAt = request.UpdatedAt
    };

    private static string GenerateNumber(string prefix) =>
        $"{prefix}-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..12].ToUpperInvariant()}";
    private static string? NormalizeOptional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static PurchaseRequestOperationResult Failure(string code, string message) => PurchaseRequestOperationResult.Failure(code, message);
    private static PurchaseRequestOperationResult NotFound() => Failure("NOT_FOUND", "Purchase request was not found.");
    private static PurchaseRequestOperationResult ForbiddenBranch() => Failure("FORBIDDEN_BRANCH", "You cannot access or modify purchase requests belonging to another branch.");
    private static PurchaseRequestOperationResult InvalidTransition() => Failure("INVALID_TRANSITION", "The purchase request is not in a valid state for this operation.");
}
