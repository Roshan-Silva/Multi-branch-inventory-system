using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MultiBranchInventory.Application.GoodsReceivedNotes.DTOs;
using MultiBranchInventory.Application.GoodsReceivedNotes.Interfaces;
using MultiBranchInventory.Domain.Enums;

namespace MultiBranchInventory.Api.Controllers;

[ApiController]
[Route("api/goods-received-notes")]
[Authorize]
public class GoodsReceivedNotesController : ControllerBase
{
    private readonly IGoodsReceivedNoteService _service;

    public GoodsReceivedNotesController(IGoodsReceivedNoteService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] Guid? branchId = null,
        [FromQuery] Guid? purchaseOrderId = null,
        [FromQuery] GoodsReceivedNoteStatus? status = null,
        [FromQuery] Guid? supplierId = null,
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        CancellationToken cancellationToken = default)
    {
        var result = await _service.GetAllAsync(
            branchId, purchaseOrderId, status, supplierId, from, to, cancellationToken);
        return result.IsSuccess ? Ok(result.GoodsReceivedNotes) : MapQueryError(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await _service.GetByIdAsync(id, cancellationToken);
        return result.IsSuccess ? Ok(result.GoodsReceivedNote) : MapError(result);
    }

    [HttpPost]
    [Authorize(Roles = $"{nameof(UserRole.SuperAdmin)},{nameof(UserRole.InventoryOfficer)}")]
    public async Task<IActionResult> Create(
        [FromBody] CreateGoodsReceivedNote request,
        CancellationToken cancellationToken)
    {
        var result = await _service.CreateAsync(request, cancellationToken);
        if (!result.IsSuccess) return MapError(result);
        return CreatedAtAction(nameof(GetById),
            new { id = result.GoodsReceivedNote!.Id }, result.GoodsReceivedNote);
    }

    [HttpPost("{id:guid}/confirm")]
    [Authorize(Roles = $"{nameof(UserRole.SuperAdmin)},{nameof(UserRole.InventoryOfficer)}")]
    public Task<IActionResult> Confirm(Guid id, CancellationToken cancellationToken) =>
        RunTransition(_service.ConfirmAsync(id, cancellationToken));

    [HttpPost("{id:guid}/cancel")]
    [Authorize(Roles = $"{nameof(UserRole.SuperAdmin)},{nameof(UserRole.InventoryOfficer)}")]
    public Task<IActionResult> Cancel(Guid id, CancellationToken cancellationToken) =>
        RunTransition(_service.CancelAsync(id, cancellationToken));

    private async Task<IActionResult> RunTransition(Task<GoodsReceivedNoteOperationResult> task)
    {
        var result = await task;
        return result.IsSuccess ? Ok(result.GoodsReceivedNote) : MapError(result);
    }

    private IActionResult MapError(GoodsReceivedNoteOperationResult result)
    {
        var response = new { message = result.ErrorMessage };
        return result.ErrorCode switch
        {
            "NOT_FOUND" or "PURCHASE_ORDER_NOT_FOUND" => NotFound(response),
            "FORBIDDEN" or "FORBIDDEN_BRANCH" => StatusCode(StatusCodes.Status403Forbidden, response),
            "INVALID_PO_STATUS" or "INVALID_STATUS" or "OVER_RECEIVED_QUANTITY" or
                "INVENTORY_NOT_CONFIGURED" => Conflict(response),
            _ => BadRequest(response)
        };
    }

    private IActionResult MapQueryError(GoodsReceivedNoteQueryResult result)
    {
        var response = new { message = result.ErrorMessage };
        return result.ErrorCode is "FORBIDDEN" or "FORBIDDEN_BRANCH"
            ? StatusCode(StatusCodes.Status403Forbidden, response)
            : BadRequest(response);
    }
}
