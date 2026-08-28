using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MultiBranchInventory.Application.Inventories.DTOs;
using MultiBranchInventory.Application.Inventories.Interfaces;
using MultiBranchInventory.Domain.Enums;

namespace MultiBranchInventory.Api.Controllers;

[ApiController]
[Route("api/inventory")]
[Authorize]
public class InventoryController : ControllerBase
{
    private readonly IInventoryService _inventoryService;

    public InventoryController(IInventoryService inventoryService)
    {
        _inventoryService = inventoryService;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<InventoryResponse>>> GetAll(
        [FromQuery] Guid? branchId = null,
        [FromQuery] Guid? productId = null,
        [FromQuery] bool lowStockOnly = false,
        CancellationToken cancellationToken = default)
    {
        var result = await _inventoryService.GetAllAsync(
            branchId,
            productId,
            lowStockOnly,
            cancellationToken);

        return result.IsSuccess
            ? Ok(result.Inventories)
            : MapQueryError(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<InventoryResponse>> GetById(
        Guid id,
        CancellationToken cancellationToken)
    {
        var result = await _inventoryService.GetByIdAsync(id, cancellationToken);
        return result.IsSuccess ? Ok(result.Inventory) : MapError(result);
    }

    [HttpPost]
    [Authorize(Roles = nameof(UserRole.SuperAdmin))]
    public async Task<ActionResult<InventoryResponse>> Create(
        [FromBody] CreateInventoryRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _inventoryService.CreateAsync(request, cancellationToken);

        if (!result.IsSuccess)
            return MapError(result);

        return CreatedAtAction(
            nameof(GetById),
            new { id = result.Inventory!.Id },
            result.Inventory);
    }

    [HttpPut("{id:guid}/levels")]
    [Authorize(Roles = $"{nameof(UserRole.SuperAdmin)},{nameof(UserRole.InventoryOfficer)}")]
    public async Task<ActionResult<InventoryResponse>> UpdateLevels(
        Guid id,
        [FromBody] UpdateInventoryLevelsRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _inventoryService.UpdateLevelsAsync(
            id,
            request,
            cancellationToken);
        return result.IsSuccess ? Ok(result.Inventory) : MapError(result);
    }

    [HttpPost("{id:guid}/adjustments")]
    [Authorize(Roles = $"{nameof(UserRole.SuperAdmin)},{nameof(UserRole.InventoryOfficer)}")]
    public async Task<ActionResult<InventoryResponse>> Adjust(
        Guid id,
        [FromBody] AdjustInventoryRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _inventoryService.AdjustAsync(id, request, cancellationToken);
        return result.IsSuccess ? Ok(result.Inventory) : MapError(result);
    }

    private ActionResult<InventoryResponse> MapError(InventoryOperationResult result)
    {
        var response = new { message = result.ErrorMessage };
        return result.ErrorCode switch
        {
            "NOT_FOUND" or "BRANCH_NOT_FOUND" or "PRODUCT_NOT_FOUND" => NotFound(response),
            "DUPLICATE_INVENTORY" => Conflict(response),
            "FORBIDDEN" or "FORBIDDEN_BRANCH" => StatusCode(StatusCodes.Status403Forbidden, response),
            _ => BadRequest(response)
        };
    }

    private ActionResult<IReadOnlyList<InventoryResponse>> MapQueryError(
        InventoryQueryResult result)
    {
        var response = new { message = result.ErrorMessage };
        return result.ErrorCode is "FORBIDDEN" or "FORBIDDEN_BRANCH"
            ? StatusCode(StatusCodes.Status403Forbidden, response)
            : BadRequest(response);
    }
}
