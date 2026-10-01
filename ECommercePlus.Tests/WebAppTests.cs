using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;

namespace ECommercePlus.Tests;

public class WebAppTests : IClassFixture<WebAppTests.Factory>
{
    private readonly Factory _factory;

    public WebAppTests(Factory factory) => _factory = factory;

    public sealed class Factory : WebApplicationFactory<Program>
    {
        private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"ecommerceplus-tests-{Guid.NewGuid():N}.db");

        protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
        {
            builder.UseSetting("ConnectionStrings:Default", $"Data Source={_dbPath}");
            builder.UseSetting("DisableHttpsRedirection", "true");
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            foreach (var file in new[] { _dbPath, _dbPath + "-wal", _dbPath + "-shm" })
                File.Delete(file);
        }
    }

    [Theory]
    [InlineData("/Shop")]
    [InlineData("/Shop?q=speaker&sort=PriceAsc&inStock=true")]
    [InlineData("/Products")]
    [InlineData("/Products/Create")]
    [InlineData("/Products/Import")]
    [InlineData("/Cart")]
    [InlineData("/Orders")]
    [InlineData("/health")]
    public async Task Pages_render(string url)
    {
        var response = await _factory.CreateClient().GetAsync(url);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Seeded_catalog_is_searchable_and_html_encoded()
    {
        var html = await _factory.CreateClient().GetStringAsync("/Shop?q=xss");

        Assert.Contains("&lt;script&gt;", html);
        Assert.DoesNotContain("<script>alert", html);
    }

    [Fact]
    public async Task Posts_without_antiforgery_token_are_rejected()
    {
        var response = await _factory.CreateClient().PostAsync("/Cart/Add", new FormUrlEncodedContent(new Dictionary<string, string> { ["productId"] = "1" }));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Unknown_product_returns_404()
    {
        var response = await _factory.CreateClient().GetAsync("/Shop/Details/999999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Security_headers_are_sent()
    {
        var response = await _factory.CreateClient().GetAsync("/Shop");

        Assert.True(response.Headers.Contains("Content-Security-Policy"));
        Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());
    }
}
