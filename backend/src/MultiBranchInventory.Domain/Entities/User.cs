using MultiBranchInventory.Domain.Common;
using MultiBranchInventory.Domain.Enums;

namespace MultiBranchInventory.Domain.Entities;

public class User : BaseEntity
{
    public string FirstName { get; set; } = string.Empty;

    public string LastName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string PasswordHash { get; set; } = string.Empty;

    public UserRole Role { get; set; }

    public Guid? BranchId { get; set; }

    public bool IsActive { get; set; } = true;

    public Branch? Branch { get; set; }
}