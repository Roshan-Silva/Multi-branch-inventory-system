using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MultiBranchInventory.Application.Categories.DTOs;
using MultiBranchInventory.Application.Categories.Interfaces;
using MultiBranchInventory.Domain.Enums;

namespace MultiBranchInventory.Api.Controllers;

[ApiController]
[Route("api/categories")]
[Authorize]
public class CategoriesController : ControllerBase
{
    private readonly ICategoryService _categoryService;

    public CategoriesController(ICategoryService categoryService)
    {
        _categoryService = categoryService;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<CategoryResponse>>> GetAll(
        [FromQuery] bool includeInactive = false,
        CancellationToken cancellationToken = default)
    {
        var categories = await _categoryService.GetAllAsync(
            includeInactive,
            cancellationToken);

        return Ok(categories);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<CategoryResponse>> GetById(
        Guid id,
        CancellationToken cancellationToken)
    {
        var category = await _categoryService.GetByIdAsync(
            id,
            cancellationToken);

        if (category is null)
        {
            return NotFound(new { message = "Category was not found." });
        }

        return Ok(category);
    }

    [HttpPost]
    [Authorize(Roles = nameof(UserRole.SuperAdmin))]
    public async Task<ActionResult<CategoryResponse>> Create(
        [FromBody] CreateCategoryRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _categoryService.CreateAsync(
            request,
            cancellationToken);

        if (!result.IsSuccess)
        {
            return MapError(result);
        }

        return CreatedAtAction(
            nameof(GetById),
            new { id = result.Category!.Id },
            result.Category);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = nameof(UserRole.SuperAdmin))]
    public async Task<ActionResult<CategoryResponse>> Update(
        Guid id,
        [FromBody] UpdateCategoryRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _categoryService.UpdateAsync(
            id,
            request,
            cancellationToken);

        return result.IsSuccess
            ? Ok(result.Category)
            : MapError(result);
    }

    [HttpPatch("{id:guid}/status")]
    [Authorize(Roles = nameof(UserRole.SuperAdmin))]
    public async Task<ActionResult<CategoryResponse>> UpdateStatus(
        Guid id,
        [FromBody] UpdateCategoryStatusRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _categoryService.UpdateStatusAsync(
            id,
            request,
            cancellationToken);

        return result.IsSuccess
            ? Ok(result.Category)
            : MapError(result);
    }

    private ActionResult<CategoryResponse> MapError(
        CategoryOperationResult result)
    {
        var response = new { message = result.ErrorMessage };

        return result.ErrorCode switch
        {
            "NOT_FOUND" => NotFound(response),
            "DUPLICATE_NAME" => Conflict(response),
            _ => BadRequest(response)
        };
    }
}
