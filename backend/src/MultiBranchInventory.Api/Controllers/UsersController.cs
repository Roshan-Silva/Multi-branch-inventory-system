using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MultiBranchInventory.Application.Users.DTOs;
using MultiBranchInventory.Application.Users.Interfaces;
using MultiBranchInventory.Domain.Enums;

namespace MultiBranchInventory.Api.Controllers;

[ApiController]
[Route("api/users")]
[Authorize(Roles = nameof(UserRole.SuperAdmin))]
public class UsersController : ControllerBase
{
    private readonly IUserService _userService;

    public UsersController(IUserService userService)
    {
        _userService = userService;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<UserResponse>>> GetAll(
        [FromQuery] bool includeInactive = false,
        CancellationToken cancellationToken = default)
    {
        var users = await _userService.GetAllAsync(
            includeInactive,
            cancellationToken);

        return Ok(users);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<UserResponse>> GetById(
        Guid id,
        CancellationToken cancellationToken)
    {
        var user = await _userService.GetByIdAsync(id, cancellationToken);

        if (user is null)
        {
            return NotFound(new { message = "User was not found." });
        }

        return Ok(user);
    }

    [HttpPost]
    public async Task<ActionResult<UserResponse>> Create(
        [FromBody] CreateUserRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _userService.CreateAsync(
            request,
            cancellationToken);

        if (!result.IsSuccess)
        {
            return MapError(result);
        }

        return CreatedAtAction(
            nameof(GetById),
            new { id = result.User!.Id },
            result.User);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<UserResponse>> Update(
        Guid id,
        [FromBody] UpdateUserRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _userService.UpdateAsync(
            id,
            request,
            cancellationToken);

        return result.IsSuccess
            ? Ok(result.User)
            : MapError(result);
    }

    [HttpPatch("{id:guid}/status")]
    public async Task<ActionResult<UserResponse>> UpdateStatus(
        Guid id,
        [FromBody] UpdateUserStatusRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _userService.UpdateStatusAsync(
            id,
            request,
            cancellationToken);

        return result.IsSuccess
            ? Ok(result.User)
            : MapError(result);
    }

    private ActionResult<UserResponse> MapError(UserOperationResult result)
    {
        var response = new { message = result.ErrorMessage };

        return result.ErrorCode switch
        {
            "NOT_FOUND" or "BRANCH_NOT_FOUND" => NotFound(response),
            "DUPLICATE_EMAIL" => Conflict(response),
            _ => BadRequest(response)
        };
    }
}
