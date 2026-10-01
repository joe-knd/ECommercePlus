using ECommercePlus.Identity;
using ECommercePlus.Infrastructure;
using ECommercePlus.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace ECommercePlus.Controllers;

public class AccountController(
    SignInManager<AppUser> signInManager,
    UserManager<AppUser> userManager,
    AdminSeeder adminSeeder,
    ILogger<AccountController> logger) : Controller
{
    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
            return RedirectToLocal(returnUrl);

        return View(new LoginModel { ReturnUrl = returnUrl });
    }

    [HttpPost]
    public async Task<IActionResult> Login(LoginModel model)
    {
        if (!ModelState.IsValid)
            return View(model);

        var result = await signInManager.PasswordSignInAsync(model.Email.Trim(), model.Password, isPersistent: false, lockoutOnFailure: true);
        if (result.Succeeded)
        {
            logger.LogInformation("User {Email} signed in", model.Email);
            return RedirectToLocal(model.ReturnUrl);
        }

        if (result.IsLockedOut)
        {
            logger.LogWarning("Sign-in attempt for locked-out account {Email}", model.Email);
            ModelState.AddModelError(string.Empty, "This account is locked. Try again later or contact an administrator.");
        }
        else
        {
            ModelState.AddModelError(string.Empty, "Invalid email or password.");
        }

        model.Password = string.Empty;
        return View(model);
    }

    [HttpPost]
    public async Task<IActionResult> Logout()
    {
        await signInManager.SignOutAsync();
        HttpContext.Session.Clear();
        return RedirectToAction("Index", "Shop");
    }

    [Authorize, HttpGet]
    public IActionResult ChangePassword() => View(new ChangePasswordModel());

    [Authorize, HttpPost]
    public async Task<IActionResult> ChangePassword(ChangePasswordModel model)
    {
        if (!ModelState.IsValid)
            return View(new ChangePasswordModel());

        var user = await userManager.GetUserAsync(User);
        if (user is null)
        {
            await signInManager.SignOutAsync();
            return RedirectToAction(nameof(Login));
        }

        if (model.NewPassword == model.CurrentPassword)
        {
            ModelState.AddModelError(nameof(model.NewPassword), "The new password must be different from the current one.");
            return View(new ChangePasswordModel());
        }

        var result = await userManager.ChangePasswordAsync(user, model.CurrentPassword, model.NewPassword);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
                ModelState.AddModelError(error.Code == "PasswordMismatch" ? nameof(model.CurrentPassword) : nameof(model.NewPassword), error.Description);
            return View(new ChangePasswordModel());
        }

        if (user.MustChangePassword)
        {
            user.MustChangePassword = false;
            await userManager.UpdateAsync(user);
        }

        if (adminSeeder.IsSeedAdmin(user))
            adminSeeder.DeletePasswordFile();

        await signInManager.RefreshSignInAsync(user);
        logger.LogInformation("User {Email} changed their password", user.Email);
        TempData[TempDataKeys.Success] = "Your password has been changed.";
        return RedirectToAction("Index", "Shop");
    }

    [HttpGet]
    public IActionResult AccessDenied() => View();

    private IActionResult RedirectToLocal(string? returnUrl) =>
        Url.IsLocalUrl(returnUrl) ? LocalRedirect(returnUrl) : RedirectToAction("Index", "Shop");
}
