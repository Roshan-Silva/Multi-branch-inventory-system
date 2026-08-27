using System.ComponentModel.DataAnnotations;
using MultiBranchInventory.Domain.Enums;

namespace MultiBranchInventory.Application.Users.DTOs;

public class UpdateUserRequest
{
    [Required]
    [MaxLength(100)]
    public string FirstName { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string LastName { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    [MaxLength(200)]
    public string Email { get; set; } = string.Empty;

    public UserRole Role { get; set; }

    public Guid? BranchId { get; set; }
}
