namespace MultiBranchInventory.Infrastructure.Authentication;

public class AdminSeedSettings
{
    public const string SectionName = "SeedAdmin";

    public string FirstName { get; set; } = string.Empty;

    public string LastName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;
}