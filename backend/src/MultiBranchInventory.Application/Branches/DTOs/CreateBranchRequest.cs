using System.ComponentModel.DataAnnotations;

namespace MultiBranchInventory.Application.Branches.DTOs;

public class CreateBranchRequest
{
    [Required]
    [MaxLength(20)]
    public string Code { get; set; } = string.Empty;

    [Required]
    [MaxLength(150)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Address { get; set; }

    [MaxLength(30)]
    public string? PhoneNumber { get; set; }

    [EmailAddress]
    [MaxLength(150)]
    public string? Email { get; set; }
}