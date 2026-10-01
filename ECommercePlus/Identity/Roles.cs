namespace ECommercePlus.Identity;

public static class Roles
{
    public const string Admin = "Admin";
}

public static class Policies
{
    public const string AdminOnly = "AdminOnly";
}

public static class AppClaims
{
    public const string MustChangePassword = "must_change_password";
}
