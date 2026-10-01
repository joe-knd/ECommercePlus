using System.Net;
using System.Text.RegularExpressions;
using ECommercePlus.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace ECommercePlus.Tests;

public partial class WebAppTests : IClassFixture<WebAppTests.Factory>
{
    private const string AdminEmail = "admin@test.local";
    private const string AdminTempPassword = "Temp-Admin-Pass-1!";

    private readonly Factory _factory;

    public WebAppTests(Factory factory) => _factory = factory;

    public sealed class Factory : WebApplicationFactory<Program>
    {
        private readonly string _dataDirectory = Path.Combine(Path.GetTempPath(), $"ecommerceplus-tests-{Guid.NewGuid():N}");

        protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
        {
            builder.UseSetting("ConnectionStrings:Default", $"Data Source={Path.Combine(_dataDirectory, "test.db")}");
            builder.UseSetting("https_port", "443");
            builder.UseSetting("AdminSeed:Email", AdminEmail);
            builder.UseSetting("AdminSeed:Password", AdminTempPassword);
            builder.UseSetting("AdminSeed:PasswordFilePath", Path.Combine(_dataDirectory, "initial-admin-password.txt"));
        }

        public HttpClient CreateHttpsClient() =>
            CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            if (Directory.Exists(_dataDirectory))
                Directory.Delete(_dataDirectory, recursive: true);
        }
    }

    [GeneratedRegex("name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"")]
    private static partial Regex AntiforgeryRegex();

    private static async Task<HttpResponseMessage> PostFormAsync(HttpClient client, string formUrl, string postUrl, Dictionary<string, string> fields)
    {
        var page = await client.GetStringAsync(formUrl);
        fields["__RequestVerificationToken"] = AntiforgeryRegex().Match(page).Groups[1].Value;
        return await client.PostAsync(postUrl, new FormUrlEncodedContent(fields));
    }

    private static Task<HttpResponseMessage> LoginAsync(HttpClient client, string email, string password) =>
        PostFormAsync(client, "/Account/Login", "/Account/Login", new() { ["Email"] = email, ["Password"] = password });

    private async Task<HttpClient> CreateUserClientAsync(bool isAdmin)
    {
        var email = $"user-{Guid.NewGuid():N}@test.local";
        const string password = "User-Password-123!";

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
            var user = new AppUser { UserName = email, Email = email, EmailConfirmed = true };
            Assert.True((await users.CreateAsync(user, password)).Succeeded);
            if (isAdmin)
                Assert.True((await users.AddToRoleAsync(user, Roles.Admin)).Succeeded);
        }

        var client = _factory.CreateHttpsClient();
        Assert.Equal(HttpStatusCode.Redirect, (await LoginAsync(client, email, password)).StatusCode);
        return client;
    }

    [Theory]
    [InlineData("/Shop")]
    [InlineData("/Shop?q=speaker&sort=PriceAsc&inStock=true")]
    [InlineData("/Cart")]
    [InlineData("/Account/Login")]
    [InlineData("/health")]
    public async Task Public_pages_render(string url)
    {
        var response = await _factory.CreateHttpsClient().GetAsync(url);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Theory]
    [InlineData("/Products")]
    [InlineData("/Products/Create")]
    [InlineData("/Products/Import")]
    [InlineData("/Orders")]
    [InlineData("/Users")]
    public async Task Admin_pages_require_sign_in(string url)
    {
        var response = await _factory.CreateHttpsClient().GetAsync(url);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.StartsWith("/Account/Login", response.Headers.Location!.PathAndQuery);
    }

    [Theory]
    [InlineData("/Products")]
    [InlineData("/Users")]
    [InlineData("/Orders")]
    public async Task Admin_pages_are_forbidden_for_non_admin_users(string url)
    {
        var client = await CreateUserClientAsync(isAdmin: false);

        var response = await client.GetAsync(url);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.StartsWith("/Account/AccessDenied", response.Headers.Location!.PathAndQuery);
    }

    [Theory]
    [InlineData("/Products")]
    [InlineData("/Products/Create")]
    [InlineData("/Products/Import")]
    [InlineData("/Orders")]
    [InlineData("/Users")]
    [InlineData("/Users/Create")]
    public async Task Admin_pages_render_for_admins(string url)
    {
        var client = await CreateUserClientAsync(isAdmin: true);

        var response = await client.GetAsync(url);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Seeded_admin_must_change_temporary_password_before_using_admin_area()
    {
        var client = _factory.CreateHttpsClient();
        var login = await LoginAsync(client, AdminEmail, AdminTempPassword);
        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);

        var blocked = await client.GetAsync("/Products");
        Assert.Equal("/Account/ChangePassword", blocked.Headers.Location!.OriginalString);

        var change = await PostFormAsync(client, "/Account/ChangePassword", "/Account/ChangePassword", new()
        {
            ["CurrentPassword"] = AdminTempPassword,
            ["NewPassword"] = "Brand-New-Admin-Pass-9!",
            ["ConfirmPassword"] = "Brand-New-Admin-Pass-9!"
        });
        Assert.Equal(HttpStatusCode.Redirect, change.StatusCode);

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/Products")).StatusCode);
    }

    [Fact]
    public async Task Invalid_credentials_are_rejected()
    {
        var response = await LoginAsync(_factory.CreateHttpsClient(), AdminEmail, "not-the-password");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Invalid email or password", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Admin_can_create_user_with_temporary_password()
    {
        var admin = await CreateUserClientAsync(isAdmin: true);
        var email = $"new-{Guid.NewGuid():N}@test.local";

        var response = await PostFormAsync(admin, "/Users/Create", "/Users/Create", new() { ["Email"] = email, ["IsAdmin"] = "false" });
        var html = await response.Content.ReadAsStringAsync();
        var password = WebUtility.HtmlDecode(Regex.Match(html, "data-testid=\"temporary-password\">([^<]+)<").Groups[1].Value);

        Assert.False(string.IsNullOrEmpty(password));
        var user = _factory.CreateHttpsClient();
        Assert.Equal(HttpStatusCode.Redirect, (await LoginAsync(user, email, password)).StatusCode);
        Assert.Equal("/Account/ChangePassword", (await user.GetAsync("/Shop")).Headers.Location!.OriginalString);
    }

    [Fact]
    public async Task Http_requests_are_redirected_to_https()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("http://localhost"), AllowAutoRedirect = false });

        var response = await client.GetAsync("/Shop");

        Assert.Equal(HttpStatusCode.TemporaryRedirect, response.StatusCode);
        Assert.Equal(Uri.UriSchemeHttps, response.Headers.Location!.Scheme);
    }

    [Fact]
    public async Task Seeded_catalog_is_searchable_and_html_encoded()
    {
        var html = await _factory.CreateHttpsClient().GetStringAsync("/Shop?q=xss");

        Assert.Contains("&lt;script&gt;", html);
        Assert.DoesNotContain("<script>alert", html);
    }

    [Fact]
    public async Task Posts_without_antiforgery_token_are_rejected()
    {
        var response = await _factory.CreateHttpsClient().PostAsync("/Cart/Add", new FormUrlEncodedContent(new Dictionary<string, string> { ["productId"] = "1" }));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Unknown_product_returns_404()
    {
        var response = await _factory.CreateHttpsClient().GetAsync("/Shop/Details/999999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Orders_of_other_customers_are_not_visible_anonymously()
    {
        var response = await _factory.CreateHttpsClient().GetAsync("/orders/ORD-20260101-DEADBEEF");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Security_headers_and_secure_cookies_are_sent()
    {
        var response = await _factory.CreateHttpsClient().GetAsync("/Account/Login");

        Assert.True(response.Headers.Contains("Content-Security-Policy"));
        Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.All(response.Headers.GetValues("Set-Cookie"), cookie => Assert.Contains("secure", cookie, StringComparison.OrdinalIgnoreCase));
    }
}
