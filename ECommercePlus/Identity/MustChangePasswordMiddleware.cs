namespace ECommercePlus.Identity;

public sealed class MustChangePasswordMiddleware(RequestDelegate next)
{
    private const string ChangePasswordPath = "/Account/ChangePassword";

    private static readonly string[] AllowedPrefixes =
    [
        ChangePasswordPath, "/Account/Logout", "/error", "/health", "/lib/", "/css/", "/js/", "/favicon.ico", "/ECommercePlus.styles.css"
    ];

    public Task InvokeAsync(HttpContext context)
    {
        if (context.User.HasClaim(AppClaims.MustChangePassword, "true") &&
            !AllowedPrefixes.Any(p => context.Request.Path.StartsWithSegments(p.TrimEnd('/'), StringComparison.OrdinalIgnoreCase)))
        {
            context.Response.Redirect(ChangePasswordPath);
            return Task.CompletedTask;
        }

        return next(context);
    }
}
