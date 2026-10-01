using ECommercePlus.Data;
using ECommercePlus.Infrastructure;
using ECommercePlus.Services.Cart;
using ECommercePlus.Services.Checkout;
using ECommercePlus.Services.Import;
using ECommercePlus.Services.Payments;
using ECommercePlus.Services.Products;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

var connectionString = ResolveSqliteConnectionString(
    builder.Configuration.GetConnectionString("Default") ?? "Data Source=App_Data/ecommerce.db",
    builder.Environment.ContentRootPath);

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddDbContext<AppDbContext>(options => options.UseSqlite(connectionString));
builder.Services.Configure<SeedOptions>(builder.Configuration.GetSection(SeedOptions.SectionName));

builder.Services.AddScoped<IProductService, ProductService>();
builder.Services.AddScoped<IProductCsvImporter, ProductCsvImporter>();
builder.Services.AddScoped<ICartStore, SessionCartStore>();
builder.Services.AddScoped<ICartService, CartService>();
builder.Services.AddScoped<ICheckoutService, CheckoutService>();
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
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.IdleTimeout = TimeSpan.FromHours(2);
});

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

if (!app.Configuration.GetValue<bool>("DisableHttpsRedirection"))
    app.UseHttpsRedirection();

app.UseRouting();
app.UseSession();
app.UseAuthorization();

app.MapStaticAssets();
app.MapHealthChecks("/health");
app.MapControllerRoute(
        name: "default",
        pattern: "{controller=Shop}/{action=Index}/{id?}")
    .WithStaticAssets();

app.Run();

static string ResolveSqliteConnectionString(string connectionString, string contentRoot)
{
    var builder = new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder(connectionString);
    if (string.IsNullOrWhiteSpace(builder.DataSource) || builder.DataSource == ":memory:")
        return connectionString;

    builder.DataSource = Path.GetFullPath(builder.DataSource, contentRoot);
    Directory.CreateDirectory(Path.GetDirectoryName(builder.DataSource)!);
    return builder.ToString();
}

public partial class Program;
