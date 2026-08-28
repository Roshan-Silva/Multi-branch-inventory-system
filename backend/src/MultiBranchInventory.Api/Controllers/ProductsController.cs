using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MultiBranchInventory.Application.Products.DTOs;
using MultiBranchInventory.Application.Products.Interfaces;
using MultiBranchInventory.Domain.Enums;

namespace MultiBranchInventory.Api.Controllers;

[ApiController]
[Route("api/products")]
[Authorize]
public class ProductsController : ControllerBase
{
    private readonly IProductService _productService;

    public ProductsController(IProductService productService)
    {
        _productService = productService;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ProductResponse>>> GetAll(
        [FromQuery] bool includeInactive = false,
        [FromQuery] Guid? categoryId = null,
        [FromQuery] string? search = null,
        CancellationToken cancellationToken = default)
    {
        var products = await _productService.GetAllAsync(
            includeInactive,
            categoryId,
            search,
            cancellationToken);

        return Ok(products);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ProductResponse>> GetById(
        Guid id,
        CancellationToken cancellationToken)
    {
        var product = await _productService.GetByIdAsync(id, cancellationToken);
        return product is null
            ? NotFound(new { message = "Product was not found." })
            : Ok(product);
    }

    [HttpPost]
    [Authorize(Roles = nameof(UserRole.SuperAdmin))]
    public async Task<ActionResult<ProductResponse>> Create(
        [FromBody] CreateProductRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _productService.CreateAsync(request, cancellationToken);

        if (!result.IsSuccess)
        {
            return MapError(result);
        }

        return CreatedAtAction(
            nameof(GetById),
            new { id = result.Product!.Id },
            result.Product);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = nameof(UserRole.SuperAdmin))]
    public async Task<ActionResult<ProductResponse>> Update(
        Guid id,
        [FromBody] UpdateProductRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _productService.UpdateAsync(id, request, cancellationToken);
        return result.IsSuccess ? Ok(result.Product) : MapError(result);
    }

    [HttpPatch("{id:guid}/status")]
    [Authorize(Roles = nameof(UserRole.SuperAdmin))]
    public async Task<ActionResult<ProductResponse>> UpdateStatus(
        Guid id,
        [FromBody] UpdateProductStatusRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _productService.UpdateStatusAsync(
            id,
            request,
            cancellationToken);

        return result.IsSuccess ? Ok(result.Product) : MapError(result);
    }

    private ActionResult<ProductResponse> MapError(ProductOperationResult result)
    {
        var response = new { message = result.ErrorMessage };

        return result.ErrorCode switch
        {
            "NOT_FOUND" or "CATEGORY_NOT_FOUND" => NotFound(response),
            "DUPLICATE_SKU" => Conflict(response),
            _ => BadRequest(response)
        };
    }
}
