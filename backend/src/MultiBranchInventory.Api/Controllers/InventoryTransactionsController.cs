using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MultiBranchInventory.Application.InventoryTransactions.DTOs;
using MultiBranchInventory.Application.InventoryTransactions.Interfaces;
using MultiBranchInventory.Domain.Enums;

namespace MultiBranchInventory.Api.Controllers;

[ApiController]
[Route("api/inventory-transactions")]
[Authorize]
public class InventoryTransactionsController : ControllerBase
{
    private readonly IInventoryTransactionService _service;

    public InventoryTransactionsController(IInventoryTransactionService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<InventoryTransactionResponse>>> GetAll(
        [FromQuery] Guid? inventoryId = null,
        [FromQuery] Guid? branchId = null,
        [FromQuery] Guid? productId = null,
        [FromQuery] InventoryTransactionType? type = null,
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        CancellationToken cancellationToken = default)
    {
        var result = await _service.GetAllAsync(
            inventoryId,
            branchId,
            productId,
            type,
            from,
            to,
            cancellationToken);

        if (result.IsSuccess)
            return Ok(result.Transactions);

        var response = new { message = result.ErrorMessage };
        return result.ErrorCode is "FORBIDDEN" or "FORBIDDEN_BRANCH"
            ? StatusCode(StatusCodes.Status403Forbidden, response)
            : BadRequest(response);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<InventoryTransactionResponse>> GetById(
        Guid id,
        CancellationToken cancellationToken)
    {
        var result = await _service.GetByIdAsync(id, cancellationToken);
        if (result.IsSuccess)
            return Ok(result.Transaction);

        var response = new { message = result.ErrorMessage };
        return result.ErrorCode switch
        {
            "NOT_FOUND" => NotFound(response),
            "FORBIDDEN" or "FORBIDDEN_BRANCH" =>
                StatusCode(StatusCodes.Status403Forbidden, response),
            _ => BadRequest(response)
        };
    }
}
