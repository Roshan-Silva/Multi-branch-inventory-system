using MultiBranchInventory.Domain.Enums;

namespace MultiBranchInventory.Application.Authentication.DTOs;

public class AuthUserDto
{
    public Guid Id { get; set; }

    public string FirstName { get; set; } = string.Empty;

    public string LastName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public UserRole Role { get; set; }

    public Guid? BranchId { get; set; }
}