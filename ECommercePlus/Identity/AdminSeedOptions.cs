namespace ECommercePlus.Identity;

public sealed class AdminSeedOptions
{
    public const string SectionName = "AdminSeed";
    public string Email { get; set; } = "admin@ecommerceplus.local";
    public string? Password { get; set; }
    public string PasswordFilePath { get; set; } = "App_Data/initial-admin-password.txt";
}
