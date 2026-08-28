using MultiBranchInventory.Domain.Enums;

namespace MultiBranchInventory.Application.Authentication.Interfaces;

public interface ICurrentUserContext
{
    Guid UserId { get; }
    UserRole Role { get; }
    Guid? BranchId { get; }
    bool IsAuthenticated { get; }
}
