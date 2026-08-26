using MultiBranchInventory.Domain.Common;

namespace MultiBranchInventory.Domain.Entities;

public class Branch : BaseEntity
{
    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? Address { get; set; }

    public string? PhoneNumber { get; set; }

    public string? Email { get; set; }

    public bool IsActive { get; set; } = true;
}