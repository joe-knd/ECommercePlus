using System.ComponentModel.DataAnnotations;

namespace ECommercePlus.ViewModels;

public sealed class LoginModel
{
    [Required, EmailAddress, StringLength(254)]
    public string Email { get; set; } = string.Empty;

    [Required, DataType(DataType.Password), StringLength(128)]
    public string Password { get; set; } = string.Empty;

    public string? ReturnUrl { get; set; }
}

public sealed class ChangePasswordModel
{
    [Required, DataType(DataType.Password), Display(Name = "Current password"), StringLength(128)]
    public string CurrentPassword { get; set; } = string.Empty;

    [Required, DataType(DataType.Password), Display(Name = "New password"), StringLength(128, MinimumLength = 12)]
    public string NewPassword { get; set; } = string.Empty;

    [Required, DataType(DataType.Password), Display(Name = "Confirm new password"), Compare(nameof(NewPassword), ErrorMessage = "The passwords do not match.")]
    public string ConfirmPassword { get; set; } = string.Empty;
}

public sealed class CreateUserModel
{
    [Required, EmailAddress, StringLength(254)]
    public string Email { get; set; } = string.Empty;

    [Display(Name = "Administrator")]
    public bool IsAdmin { get; set; }
}

public sealed record UserListItem(
    string Id,
    string Email,
    bool IsAdmin,
    bool IsLockedOut,
    bool MustChangePassword,
    DateTime CreatedAtUtc,
    bool IsCurrentUser);

public sealed record TemporaryPasswordViewModel(string Title, string Email, string Password);
