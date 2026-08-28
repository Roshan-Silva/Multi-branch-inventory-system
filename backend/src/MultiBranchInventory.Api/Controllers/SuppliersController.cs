using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MultiBranchInventory.Application.Suppliers.DTOs;
using MultiBranchInventory.Application.Suppliers.Interfaces;
using MultiBranchInventory.Domain.Enums;

namespace MultiBranchInventory.Api.Controllers;

[ApiController]
[Route("api/suppliers")]
[Authorize]
public class SuppliersController : ControllerBase
{
    private readonly ISupplierService _supplierService;

    public SuppliersController(ISupplierService supplierService)
    {
        _supplierService = supplierService;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<SupplierResponse>>> GetAll(
        [FromQuery] bool includeInactive = false,
        CancellationToken cancellationToken = default)
    {
        var suppliers = await _supplierService.GetAllAsync(
            includeInactive,
            cancellationToken);
        return Ok(suppliers);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<SupplierResponse>> GetById(
        Guid id,
        CancellationToken cancellationToken)
    {
        var supplier = await _supplierService.GetByIdAsync(id, cancellationToken);
        return supplier is null
            ? NotFound(new { message = "Supplier was not found." })
            : Ok(supplier);
    }

    [HttpPost]
    [Authorize(Roles = nameof(UserRole.SuperAdmin))]
    public async Task<ActionResult<SupplierResponse>> Create(
        [FromBody] CreateSupplierRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _supplierService.CreateAsync(request, cancellationToken);

        if (!result.IsSuccess)
        {
            return MapError(result);
        }

        return CreatedAtAction(
            nameof(GetById),
            new { id = result.Supplier!.Id },
            result.Supplier);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = nameof(UserRole.SuperAdmin))]
    public async Task<ActionResult<SupplierResponse>> Update(
        Guid id,
        [FromBody] UpdateSupplierRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _supplierService.UpdateAsync(id, request, cancellationToken);
        return result.IsSuccess ? Ok(result.Supplier) : MapError(result);
    }

    [HttpPatch("{id:guid}/status")]
    [Authorize(Roles = nameof(UserRole.SuperAdmin))]
    public async Task<ActionResult<SupplierResponse>> UpdateStatus(
        Guid id,
        [FromBody] UpdateSupplierStatusRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _supplierService.UpdateStatusAsync(
            id,
            request,
            cancellationToken);
        return result.IsSuccess ? Ok(result.Supplier) : MapError(result);
    }

    private ActionResult<SupplierResponse> MapError(SupplierOperationResult result)
    {
        var response = new { message = result.ErrorMessage };

        return result.ErrorCode switch
        {
            "NOT_FOUND" => NotFound(response),
            "DUPLICATE_CODE" => Conflict(response),
            _ => BadRequest(response)
        };
    }
}
