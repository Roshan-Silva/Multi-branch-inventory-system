using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MultiBranchInventory.Application.PurchaseRequests.DTOs;
using MultiBranchInventory.Application.PurchaseRequests.Interfaces;
using MultiBranchInventory.Domain.Enums;

namespace MultiBranchInventory.Api.Controllers;

[ApiController]
[Route("api/purchase-requests")]
[Authorize]
public class PurchaseRequestsController : ControllerBase
{
    private readonly IPurchaseRequestService _service;
    public PurchaseRequestsController(IPurchaseRequestService service) { _service = service; }

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] Guid? branchId = null,
        [FromQuery] PurchaseRequestStatus? status = null,
        [FromQuery] Guid? productId = null,
        CancellationToken cancellationToken = default)
    {
        var result = await _service.GetAllAsync(branchId, status, productId, cancellationToken);
        return result.IsSuccess ? Ok(result.PurchaseRequests) : MapQueryError(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await _service.GetByIdAsync(id, cancellationToken);
        return result.IsSuccess ? Ok(result.PurchaseRequest) : MapError(result);
    }

    [HttpPost]
    [Authorize(Roles = $"{nameof(UserRole.SuperAdmin)},{nameof(UserRole.InventoryOfficer)}")]
    public async Task<IActionResult> Create([FromBody] CreatePurchaseRequest request, CancellationToken cancellationToken)
    {
        var result = await _service.CreateAsync(request, cancellationToken);
        if (!result.IsSuccess) return MapError(result);
        return CreatedAtAction(nameof(GetById), new { id = result.PurchaseRequest!.Id }, result.PurchaseRequest);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = $"{nameof(UserRole.SuperAdmin)},{nameof(UserRole.InventoryOfficer)}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdatePurchaseRequest request, CancellationToken cancellationToken)
    {
        var result = await _service.UpdateAsync(id, request, cancellationToken);
        return result.IsSuccess ? Ok(result.PurchaseRequest) : MapError(result);
    }

    [HttpPost("{id:guid}/submit")]
    [Authorize(Roles = $"{nameof(UserRole.SuperAdmin)},{nameof(UserRole.InventoryOfficer)}")]
    public async Task<IActionResult> Submit(Guid id, CancellationToken cancellationToken)
    {
        var result = await _service.SubmitAsync(id, cancellationToken);
        return result.IsSuccess ? Ok(result.PurchaseRequest) : MapError(result);
    }

    [HttpPost("{id:guid}/approve")]
    [Authorize(Roles = $"{nameof(UserRole.SuperAdmin)},{nameof(UserRole.BranchManager)}")]
    public async Task<IActionResult> Approve(Guid id, CancellationToken cancellationToken)
    {
        var result = await _service.ApproveAsync(id, cancellationToken);
        return result.IsSuccess ? Ok(result.PurchaseRequest) : MapError(result);
    }

    [HttpPost("{id:guid}/reject")]
    [Authorize(Roles = $"{nameof(UserRole.SuperAdmin)},{nameof(UserRole.BranchManager)}")]
    public async Task<IActionResult> Reject(Guid id, [FromBody] RejectPurchaseRequest request, CancellationToken cancellationToken)
    {
        var result = await _service.RejectAsync(id, request, cancellationToken);
        return result.IsSuccess ? Ok(result.PurchaseRequest) : MapError(result);
    }

    [HttpPost("{id:guid}/cancel")]
    [Authorize(Roles = $"{nameof(UserRole.SuperAdmin)},{nameof(UserRole.InventoryOfficer)}")]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken cancellationToken)
    {
        var result = await _service.CancelAsync(id, cancellationToken);
        return result.IsSuccess ? Ok(result.PurchaseRequest) : MapError(result);
    }

    private IActionResult MapError(PurchaseRequestOperationResult result)
    {
        var response = new { message = result.ErrorMessage };
        return result.ErrorCode switch
        {
            "NOT_FOUND" or "BRANCH_NOT_FOUND" or "PRODUCT_NOT_FOUND" => NotFound(response),
            "FORBIDDEN" or "FORBIDDEN_BRANCH" => StatusCode(403, response),
            "INVALID_TRANSITION" or "INVALID_STATUS" => Conflict(response),
            _ => BadRequest(response)
        };
    }

    private IActionResult MapQueryError(PurchaseRequestQueryResult result)
    {
        var response = new { message = result.ErrorMessage };
        return result.ErrorCode is "FORBIDDEN" or "FORBIDDEN_BRANCH"
            ? StatusCode(403, response) : BadRequest(response);
    }
}
