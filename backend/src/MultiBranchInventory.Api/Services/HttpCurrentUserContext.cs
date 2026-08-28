using System.Security.Claims;
using MultiBranchInventory.Application.Authentication.Interfaces;
using MultiBranchInventory.Domain.Enums;

namespace MultiBranchInventory.Api.Services;

public class HttpCurrentUserContext : ICurrentUserContext
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public HttpCurrentUserContext(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    private ClaimsPrincipal? User => _httpContextAccessor.HttpContext?.User;

    public bool IsAuthenticated => User?.Identity?.IsAuthenticated == true;

    public Guid UserId => Guid.TryParse(
        User?.FindFirstValue(ClaimTypes.NameIdentifier),
        out var userId) ? userId : Guid.Empty;

    public UserRole Role => Enum.TryParse<UserRole>(
        User?.FindFirstValue(ClaimTypes.Role),
        out var role) ? role : default;

    public Guid? BranchId => Guid.TryParse(
        User?.FindFirstValue("branchId"),
        out var branchId) ? branchId : null;
}
