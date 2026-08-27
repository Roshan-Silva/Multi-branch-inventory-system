using MultiBranchInventory.Domain.Enums;

namespace MultiBranchInventory.Application.Users.DTOs;

public class UserResponse
{
    public Guid Id { get; set; }

    public string FirstName { get; set; } = string.Empty;

    public string LastName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public UserRole Role { get; set; }

    public Guid? BranchId { get; set; }

    public string? BranchName { get; set; }

    public bool IsActive { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }
}
