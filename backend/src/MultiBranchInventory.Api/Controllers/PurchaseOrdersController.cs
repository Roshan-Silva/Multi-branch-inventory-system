using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MultiBranchInventory.Application.PurchaseOrders.DTOs;
using MultiBranchInventory.Application.PurchaseOrders.Interfaces;
using MultiBranchInventory.Domain.Enums;

namespace MultiBranchInventory.Api.Controllers;

[ApiController]
[Route("api/purchase-orders")]
[Authorize]
public class PurchaseOrdersController : ControllerBase
{
    private readonly IPurchaseOrderService _service;
    public PurchaseOrdersController(IPurchaseOrderService service) { _service = service; }

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] Guid? branchId = null, [FromQuery] Guid? supplierId = null,
        [FromQuery] PurchaseOrderStatus? status = null, [FromQuery] Guid? purchaseRequestId = null,
        CancellationToken cancellationToken = default)
    {
        var result = await _service.GetAllAsync(branchId, supplierId, status, purchaseRequestId, cancellationToken);
        return result.IsSuccess ? Ok(result.PurchaseOrders) : MapQueryError(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await _service.GetByIdAsync(id, cancellationToken);
        return result.IsSuccess ? Ok(result.PurchaseOrder) : MapError(result);
    }

    [HttpPost]
    [Authorize(Roles = $"{nameof(UserRole.SuperAdmin)},{nameof(UserRole.ProcurementOfficer)}")]
    public async Task<IActionResult> Create([FromBody] CreatePurchaseOrder request, CancellationToken cancellationToken)
    {
        var result = await _service.CreateAsync(request, cancellationToken);
        if (!result.IsSuccess) return MapError(result);
        return CreatedAtAction(nameof(GetById), new { id = result.PurchaseOrder!.Id }, result.PurchaseOrder);
    }

    [HttpPost("{id:guid}/submit")]
    [Authorize(Roles = $"{nameof(UserRole.SuperAdmin)},{nameof(UserRole.ProcurementOfficer)}")]
    public Task<IActionResult> Submit(Guid id, CancellationToken token) => RunTransition(_service.SubmitAsync(id, token));

    [HttpPost("{id:guid}/approve")]
    [Authorize(Roles = nameof(UserRole.SuperAdmin))]
    public Task<IActionResult> Approve(Guid id, CancellationToken token) => RunTransition(_service.ApproveAsync(id, token));

    [HttpPost("{id:guid}/send")]
    [Authorize(Roles = $"{nameof(UserRole.SuperAdmin)},{nameof(UserRole.ProcurementOfficer)}")]
    public Task<IActionResult> Send(Guid id, CancellationToken token) => RunTransition(_service.SendAsync(id, token));

    [HttpPost("{id:guid}/confirm")]
    [Authorize(Roles = $"{nameof(UserRole.SuperAdmin)},{nameof(UserRole.ProcurementOfficer)}")]
    public Task<IActionResult> Confirm(Guid id, CancellationToken token) => RunTransition(_service.ConfirmAsync(id, token));

    [HttpPost("{id:guid}/cancel")]
    [Authorize(Roles = $"{nameof(UserRole.SuperAdmin)},{nameof(UserRole.ProcurementOfficer)}")]
    public Task<IActionResult> Cancel(Guid id, CancellationToken token) => RunTransition(_service.CancelAsync(id, token));

    private async Task<IActionResult> RunTransition(Task<PurchaseOrderOperationResult> task)
    {
        var result = await task;
        return result.IsSuccess ? Ok(result.PurchaseOrder) : MapError(result);
    }

    private IActionResult MapError(PurchaseOrderOperationResult result)
    {
        var response = new { message = result.ErrorMessage };
        return result.ErrorCode switch
        {
            "NOT_FOUND" or "PURCHASE_REQUEST_NOT_FOUND" or "SUPPLIER_NOT_FOUND" => NotFound(response),
            "FORBIDDEN" or "FORBIDDEN_BRANCH" => StatusCode(403, response),
            "INVALID_TRANSITION" or "OVER_ALLOCATED_QUANTITY" => Conflict(response),
            _ => BadRequest(response)
        };
    }

    private IActionResult MapQueryError(PurchaseOrderQueryResult result)
    {
        var response = new { message = result.ErrorMessage };
        return result.ErrorCode is "FORBIDDEN" or "FORBIDDEN_BRANCH"
            ? StatusCode(403, response) : BadRequest(response);
    }
}
