using ECommercePlus.Identity;
using ECommercePlus.Infrastructure;
using ECommercePlus.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ECommercePlus.Controllers;

[Authorize(Policy = Policies.AdminOnly)]
public class UsersController(
    UserManager<AppUser> userManager,
    TimeProvider timeProvider,
    ILogger<UsersController> logger) : Controller
{
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var admins = (await userManager.GetUsersInRoleAsync(Roles.Admin)).Select(u => u.Id).ToHashSet();
        var currentUserId = userManager.GetUserId(User);
        var now = timeProvider.GetUtcNow();

        var users = await userManager.Users.AsNoTracking().OrderBy(u => u.Email).ToListAsync(cancellationToken);
        return View(users.Select(u => new UserListItem(
            u.Id,
            u.Email ?? u.UserName ?? u.Id,
            admins.Contains(u.Id),
            u.LockoutEnd is { } end && end > now,
            u.MustChangePassword,
            u.CreatedAtUtc,
            u.Id == currentUserId)).ToList());
    }

    [HttpGet]
    public IActionResult Create() => View(new CreateUserModel());

    [HttpPost]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public async Task<IActionResult> Create(CreateUserModel model)
    {
        if (!ModelState.IsValid)
            return View(model);

        var email = model.Email.Trim();
        var password = TemporaryPassword.Generate();
        var user = new AppUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            MustChangePassword = true,
            CreatedAtUtc = timeProvider.GetUtcNow().UtcDateTime
        };

        var result = await userManager.CreateAsync(user, password);
        if (result.Succeeded && model.IsAdmin)
            result = await userManager.AddToRoleAsync(user, Roles.Admin);

        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
                ModelState.AddModelError(error.Code.Contains("Email") || error.Code.Contains("UserName") ? nameof(model.Email) : string.Empty, error.Description);
            return View(model);
        }

        logger.LogInformation("User {Email} created by {Admin} (admin: {IsAdmin})", email, User.Identity?.Name, model.IsAdmin);
        return View("TemporaryPassword", new TemporaryPasswordViewModel("User created", email, password));
    }

    [HttpPost]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public async Task<IActionResult> ResetPassword(string id)
    {
        var user = await FindOtherUserAsync(id);
        if (user is null)
            return NotFound();

        var password = TemporaryPassword.Generate();
        var token = await userManager.GeneratePasswordResetTokenAsync(user);
        var result = await userManager.ResetPasswordAsync(user, token, password);
        if (!result.Succeeded)
            return Fail(string.Join(" ", result.Errors.Select(e => e.Description)));

        user.MustChangePassword = true;
        await userManager.UpdateAsync(user);
        await userManager.SetLockoutEndDateAsync(user, null);
        await userManager.ResetAccessFailedCountAsync(user);

        logger.LogInformation("Password for {Email} reset by {Admin}", user.Email, User.Identity?.Name);
        return View("TemporaryPassword", new TemporaryPasswordViewModel("Password reset", user.Email!, password));
    }

    [HttpPost]
    public async Task<IActionResult> ToggleAdmin(string id)
    {
        var user = await FindOtherUserAsync(id);
        if (user is null)
            return NotFound();

        IdentityResult result;
        if (await userManager.IsInRoleAsync(user, Roles.Admin))
        {
            if (await IsLastAdminAsync())
                return Fail("You cannot remove the last administrator.");
            result = await userManager.RemoveFromRoleAsync(user, Roles.Admin);
        }
        else
        {
            result = await userManager.AddToRoleAsync(user, Roles.Admin);
        }

        if (!result.Succeeded)
            return Fail(string.Join(" ", result.Errors.Select(e => e.Description)));

        await userManager.UpdateSecurityStampAsync(user);
        logger.LogInformation("Admin role toggled for {Email} by {Admin}", user.Email, User.Identity?.Name);
        TempData[TempDataKeys.Success] = $"Roles updated for {user.Email}.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> ToggleLock(string id)
    {
        var user = await FindOtherUserAsync(id);
        if (user is null)
            return NotFound();

        var locked = user.LockoutEnd is { } end && end > timeProvider.GetUtcNow();
        if (!locked && await userManager.IsInRoleAsync(user, Roles.Admin) && await IsLastAdminAsync())
            return Fail("You cannot lock the last administrator.");

        await userManager.SetLockoutEnabledAsync(user, true);
        await userManager.SetLockoutEndDateAsync(user, locked ? null : DateTimeOffset.MaxValue);
        await userManager.UpdateSecurityStampAsync(user);

        logger.LogInformation("User {Email} {Action} by {Admin}", user.Email, locked ? "unlocked" : "locked", User.Identity?.Name);
        TempData[TempDataKeys.Success] = $"{user.Email} has been {(locked ? "unlocked" : "locked")}.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Delete(string id)
    {
        var user = await FindOtherUserAsync(id);
        return user is null ? NotFound() : View(user);
    }

    [HttpPost, ActionName(nameof(Delete))]
    public async Task<IActionResult> DeleteConfirmed(string id)
    {
        var user = await FindOtherUserAsync(id);
        if (user is null)
            return NotFound();

        if (await userManager.IsInRoleAsync(user, Roles.Admin) && await IsLastAdminAsync())
            return Fail("You cannot delete the last administrator.");

        var result = await userManager.DeleteAsync(user);
        if (!result.Succeeded)
            return Fail(string.Join(" ", result.Errors.Select(e => e.Description)));

        logger.LogInformation("User {Email} deleted by {Admin}", user.Email, User.Identity?.Name);
        TempData[TempDataKeys.Success] = $"{user.Email} has been deleted.";
        return RedirectToAction(nameof(Index));
    }

    private async Task<AppUser?> FindOtherUserAsync(string id)
    {
        if (string.IsNullOrEmpty(id) || id == userManager.GetUserId(User))
            return null;
        return await userManager.FindByIdAsync(id);
    }

    private async Task<bool> IsLastAdminAsync() =>
        (await userManager.GetUsersInRoleAsync(Roles.Admin)).Count(u => !(u.LockoutEnd is { } end && end > timeProvider.GetUtcNow())) <= 1;

    private IActionResult Fail(string message)
    {
        TempData[TempDataKeys.Error] = message;
        return RedirectToAction(nameof(Index));
    }
}
