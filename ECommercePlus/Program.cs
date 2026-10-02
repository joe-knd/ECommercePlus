using ECommercePlus.Data;
using ECommercePlus.Identity;
using ECommercePlus.Infrastructure;
using ECommercePlus.Services.Cart;
using ECommercePlus.Services.Checkout;
using ECommercePlus.Services.Import;
using ECommercePlus.Services.Payments;
using ECommercePlus.Services.Products;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

ConfigureSelfSignedCertificateFallback(builder);

var connectionString = ResolveSqliteConnectionString(
    builder.Configuration.GetConnectionString("Default") ?? "Data Source=App_Data/ecommerce.db",
    builder.Environment.ContentRootPath);

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddDbContext<AppDbContext>(options => options.UseSqlite(connectionString));
builder.Services.Configure<SeedOptions>(builder.Configuration.GetSection(SeedOptions.SectionName));
builder.Services.Configure<AdminSeedOptions>(builder.Configuration.GetSection(AdminSeedOptions.SectionName));

builder.Services
    .AddIdentity<AppUser, IdentityRole>(options =>
    {
        options.User.RequireUniqueEmail = true;
        options.Password.RequiredLength = 12;
        options.Password.RequireDigit = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireUppercase = true;
        options.Password.RequireNonAlphanumeric = true;
        options.Lockout.AllowedForNewUsers = true;
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
    })
    .AddEntityFrameworkStores<AppDbContext>()
    .AddDefaultTokenProviders()
    .AddClaimsPrincipalFactory<AppUserClaimsPrincipalFactory>();

builder.Services.Configure<SecurityStampValidatorOptions>(options => options.ValidationInterval = TimeSpan.FromMinutes(1));

builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.Name = ".ECommercePlus.Auth";
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.LoginPath = "/Account/Login";
    options.LogoutPath = "/Account/Logout";
    options.AccessDeniedPath = "/Account/AccessDenied";
    options.ExpireTimeSpan = TimeSpan.FromHours(8);
    options.SlidingExpiration = true;
});

builder.Services.AddAuthorizationBuilder()
    .AddPolicy(Policies.AdminOnly, policy => policy.RequireAuthenticatedUser().RequireRole(Roles.Admin));

builder.Services.AddScoped<AdminSeeder>();
builder.Services.AddScoped<IProductService, ProductService>();
builder.Services.AddScoped<IProductCsvImporter, ProductCsvImporter>();
builder.Services.AddScoped<ICartStore, SessionCartStore>();
builder.Services.AddScoped<ICartService, CartService>();
builder.Services.AddScoped<ICheckoutService, CheckoutService>();
builder.Services.AddScoped<IOrderHistory, SessionOrderHistory>();
builder.Services.AddSingleton<IPaymentGateway, FakePaymentGateway>();

var keysDirectory = builder.Configuration["DataProtection:KeysPath"];
if (!string.IsNullOrWhiteSpace(keysDirectory))
    builder.Services.AddDataProtection().PersistKeysToFileSystem(new DirectoryInfo(keysDirectory));

builder.Services.AddHttpContextAccessor();
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.Cookie.Name = ".ECommercePlus.Session";
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.IdleTimeout = TimeSpan.FromHours(2);
});
builder.Services.AddAntiforgery(options => options.Cookie.SecurePolicy = CookieSecurePolicy.Always);
builder.Services.Configure<CookieTempDataProviderOptions>(options => options.Cookie.SecurePolicy = CookieSecurePolicy.Always);

builder.Services.AddControllersWithViews(options =>
    options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute()));
builder.Services.AddHealthChecks().AddDbContextCheck<AppDbContext>();

var app = builder.Build();

await DatabaseInitializer.InitializeAsync(app.Services);

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/error");
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/error/{0}");
app.UseMiddleware<SecurityHeadersMiddleware>();
app.UseHttpsRedirection();

app.UseRouting();
app.UseSession();
app.UseAuthentication();
app.UseMiddleware<MustChangePasswordMiddleware>();
app.UseAuthorization();

app.MapStaticAssets();
app.MapHealthChecks("/health");
app.MapControllerRoute(
        name: "default",
        pattern: "{controller=Shop}/{action=Index}/{id?}")
    .WithStaticAssets();

app.Lifetime.ApplicationStarted.Register(() => LogBrowseUrls(app));

app.Run();

static void ConfigureSelfSignedCertificateFallback(WebApplicationBuilder builder)
{
    var path = builder.Configuration["Https:SelfSignedCertificatePath"];
    if (string.IsNullOrWhiteSpace(path) || !string.IsNullOrWhiteSpace(builder.Configuration["Kestrel:Certificates:Default:Path"]))
        return;

    var certificate = SelfSignedCertificate.LoadOrCreate(Path.GetFullPath(path, builder.Environment.ContentRootPath));
    builder.WebHost.ConfigureKestrel(kestrel => kestrel.ConfigureHttpsDefaults(https => https.ServerCertificate = certificate));
}

static string ResolveSqliteConnectionString(string connectionString, string contentRoot)
{
    var builder = new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder(connectionString);
    if (string.IsNullOrWhiteSpace(builder.DataSource) || builder.DataSource == ":memory:")
        return connectionString;

    builder.DataSource = Path.GetFullPath(builder.DataSource, contentRoot);
    Directory.CreateDirectory(Path.GetDirectoryName(builder.DataSource)!);
    return builder.ToString();
}

static void LogBrowseUrls(WebApplication app)
{
    var browseUrls = app.Urls
        .Select(url => System.Text.RegularExpressions.Regex.Replace(url, @"://(\[::\]|0\.0\.0\.0|\+|\*)(?=[:/]|$)", "://localhost"))
        .Select(url => Uri.TryCreate(url, UriKind.Absolute, out var uri) ? uri : null)
        .Where(uri => uri is not null && uri.Scheme == Uri.UriSchemeHttps)
        .Select(uri => uri!.GetLeftPart(UriPartial.Authority))
        .Distinct()
        .ToList();

    if (browseUrls.Count > 0)
        app.Logger.LogInformation("ECommercePlus is ready. Open {Urls} in your browser.", string.Join(", ", browseUrls));
}

public partial class Program;
