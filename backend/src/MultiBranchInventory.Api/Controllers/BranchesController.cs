using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MultiBranchInventory.Application.Branches.DTOs;
using MultiBranchInventory.Application.Branches.Interfaces;
using MultiBranchInventory.Domain.Enums;

namespace MultiBranchInventory.Api.Controllers;

[ApiController]
[Route("api/branches")]
[Authorize]
public class BranchesController : ControllerBase
{
    private readonly IBranchService _branchService;

    public BranchesController(IBranchService branchService)
    {
        _branchService = branchService;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<BranchResponse>>> GetAll(
        [FromQuery] bool includeInactive = false,
        CancellationToken cancellationToken = default)
    {
        var branches = await _branchService.GetAllAsync(
            includeInactive,
            cancellationToken);

        return Ok(branches);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<BranchResponse>> GetById(
        Guid id,
        CancellationToken cancellationToken)
    {
        var branch = await _branchService.GetByIdAsync(
            id,
            cancellationToken);

        if (branch is null)
        {
            return NotFound(new
            {
                message = "Branch was not found."
            });
        }

        return Ok(branch);
    }

    [HttpPost]
    [Authorize(Roles = nameof(UserRole.SuperAdmin))]
    public async Task<ActionResult<BranchResponse>> Create(
        [FromBody] CreateBranchRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _branchService.CreateAsync(
            request,
            cancellationToken);

        if (!result.IsSuccess)
        {
            if (result.ErrorCode == "DUPLICATE_CODE")
            {
                return Conflict(new
                {
                    message = result.ErrorMessage
                });
            }

            return BadRequest(new
            {
                message = result.ErrorMessage
            });
        }

        return CreatedAtAction(
            nameof(GetById),
            new { id = result.Branch!.Id },
            result.Branch);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = nameof(UserRole.SuperAdmin))]
    public async Task<ActionResult<BranchResponse>> Update(
        Guid id,
        [FromBody] UpdateBranchRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _branchService.UpdateAsync(
            id,
            request,
            cancellationToken);

        if (!result.IsSuccess)
        {
            if (result.ErrorCode == "NOT_FOUND")
            {
                return NotFound(new
                {
                    message = result.ErrorMessage
                });
            }

            if (result.ErrorCode == "DUPLICATE_CODE")
            {
                return Conflict(new
                {
                    message = result.ErrorMessage
                });
            }

            return BadRequest(new
            {
                message = result.ErrorMessage
            });
        }

        return Ok(result.Branch);
    }

    [HttpPatch("{id:guid}/status")]
    [Authorize(Roles = nameof(UserRole.SuperAdmin))]
    public async Task<ActionResult<BranchResponse>> UpdateStatus(
        Guid id,
        [FromBody] UpdateBranchStatusRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _branchService.UpdateStatusAsync(
            id,
            request,
            cancellationToken);

        if (!result.IsSuccess)
        {
            if (result.ErrorCode == "NOT_FOUND")
            {
                return NotFound(new
                {
                    message = result.ErrorMessage
                });
            }

            return BadRequest(new
            {
                message = result.ErrorMessage
            });
        }

        return Ok(result.Branch);
    }
}