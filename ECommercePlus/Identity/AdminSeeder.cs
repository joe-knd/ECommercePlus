using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace ECommercePlus.Identity;

public sealed class AdminSeeder(
    UserManager<AppUser> userManager,
    RoleManager<IdentityRole> roleManager,
    IOptions<AdminSeedOptions> options,
    IWebHostEnvironment environment,
    TimeProvider timeProvider,
    ILogger<AdminSeeder> logger)
{
    public string PasswordFilePath => Path.GetFullPath(options.Value.PasswordFilePath, environment.ContentRootPath);

    public async Task SeedAsync()
    {
        if (!await roleManager.RoleExistsAsync(Roles.Admin))
            EnsureSucceeded(await roleManager.CreateAsync(new IdentityRole(Roles.Admin)), "create the Admin role");

        if ((await userManager.GetUsersInRoleAsync(Roles.Admin)).Count > 0)
            return;

        var seed = options.Value;
        var configuredPassword = !string.IsNullOrWhiteSpace(seed.Password);
        var password = configuredPassword ? seed.Password! : TemporaryPassword.Generate();

        var user = await userManager.FindByEmailAsync(seed.Email);
        if (user is null)
        {
            user = new AppUser
            {
                UserName = seed.Email,
                Email = seed.Email,
                EmailConfirmed = true,
                MustChangePassword = true,
                CreatedAtUtc = timeProvider.GetUtcNow().UtcDateTime
            };
            EnsureSucceeded(await userManager.CreateAsync(user, password), "create the initial admin user");
        }
        else
        {
            user.MustChangePassword = true;
            EnsureSucceeded(await userManager.RemovePasswordAsync(user), "reset the initial admin password");
            EnsureSucceeded(await userManager.AddPasswordAsync(user, password), "reset the initial admin password");
        }

        EnsureSucceeded(await userManager.AddToRoleAsync(user, Roles.Admin), "assign the Admin role");

        if (configuredPassword)
        {
            logger.LogWarning("Initial admin account {Email} created with the configured temporary password. It must be changed at first sign-in.", seed.Email);
            return;
        }

        await WritePasswordFileAsync(seed.Email, password);
        logger.LogWarning(
            "Initial admin account {Email} created. Its temporary password was written to {Path}. It must be changed at first sign-in.",
            seed.Email, PasswordFilePath);
    }

    public void DeletePasswordFile()
    {
        if (File.Exists(PasswordFilePath))
            File.Delete(PasswordFilePath);
    }

    public bool IsSeedAdmin(AppUser user) =>
        string.Equals(user.Email, options.Value.Email, StringComparison.OrdinalIgnoreCase);

    private async Task WritePasswordFileAsync(string email, string password)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(PasswordFilePath)!);
        await File.WriteAllTextAsync(PasswordFilePath,
            $"Email: {email}{Environment.NewLine}Temporary password: {password}{Environment.NewLine}" +
            $"You will be asked to change it at first sign-in; this file is deleted afterwards.{Environment.NewLine}");

        if (!OperatingSystem.IsWindows())
            File.SetUnixFileMode(PasswordFilePath, UnixFileMode.UserRead | UnixFileMode.UserWrite);
    }

    private static void EnsureSucceeded(IdentityResult result, string action)
    {
        if (!result.Succeeded)
            throw new InvalidOperationException($"Failed to {action}: {string.Join(" ", result.Errors.Select(e => e.Description))}");
    }
}
