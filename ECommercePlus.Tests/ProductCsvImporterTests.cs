using System.Text;
using ECommercePlus.Domain;
using ECommercePlus.Services.Import;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace ECommercePlus.Tests;

public class ProductCsvImporterTests : IDisposable
{
    private readonly TestDatabase _database = new();

    public void Dispose() => _database.Dispose();

    private async Task<ImportReport> ImportAsync(Stream stream)
    {
        await using var db = _database.CreateContext();
        return await new ProductCsvImporter(db, NullLogger<ProductCsvImporter>.Instance).ImportAsync(stream);
    }

    private Task<ImportReport> ImportAsync(string csv) => ImportAsync(new MemoryStream(Encoding.UTF8.GetBytes(csv)));

    private Task<ImportReport> ImportSampleAsync() =>
        ImportAsync(File.OpenRead(Path.Combine(AppContext.BaseDirectory, "TestData", "sample-products.csv")));

    private async Task<Product?> FindAsync(string sku)
    {
        await using var db = _database.CreateContext();
        return await db.Products.SingleOrDefaultAsync(p => p.Sku == sku);
    }

    [Fact]
    public async Task Sample_file_imports_valid_rows_and_reports_invalid_ones()
    {
        var report = await ImportSampleAsync();

        Assert.True(report.Succeeded);
        Assert.Equal(97, report.TotalRows);
        Assert.Equal(88, report.Created);
        Assert.Equal(4, report.Rejected);
        Assert.Equal(5, report.Ignored);
        Assert.Equal(report.TotalRows, report.Created + report.Updated + report.Unchanged + report.Rejected + report.Ignored);

        var rejectedSkus = report.Issues.Where(i => i.Severity == ImportIssueSeverity.Error).Select(i => i.Sku).ToHashSet();
        Assert.Equal(["YM-015", "DL-007", "HD-099", "WS-001"], rejectedSkus);
    }

    [Fact]
    public async Task Sample_file_edge_cases_are_normalized()
    {
        await ImportSampleAsync();

        Assert.Equal(29.99m, (await FindAsync("WM-042"))!.Price);
        Assert.Equal(ProductRules.DefaultCategory, (await FindAsync("GC-025"))!.Category);
        Assert.Null((await FindAsync("GK-088"))!.WeightKg);
        Assert.Equal(0m, (await FindAsync("MB-001"))!.Price);
        Assert.Equal("<script>alert('xss')</script>", (await FindAsync("XS-001"))!.Name);
        Assert.Equal("Robert'); DROP TABLE products;--", (await FindAsync("SQL-001"))!.Name);
        Assert.Equal("Comma, In Product Name", (await FindAsync("CI-001"))!.Name);
        Assert.Equal("Quote \"Inside\" Name", (await FindAsync("QI-001"))!.Name);
        Assert.Contains("™", (await FindAsync("WB-033"))!.Description);
        Assert.Null(await FindAsync("YM-015"));
        Assert.Null(await FindAsync("DL-007"));
    }

    [Fact]
    public async Task Duplicate_skus_in_file_use_the_last_row()
    {
        await ImportSampleAsync();

        var shoes = await FindAsync("RS-001");
        Assert.Equal(94.99m, shoes!.Price);
        Assert.Equal(120, shoes.Stock);

        var speaker = await FindAsync("BS-021");
        Assert.Equal(59.99m, speaker!.Price);
        Assert.Equal(0.8m, speaker.WeightKg);
    }

    [Fact]
    public async Task Reimporting_the_same_file_is_idempotent()
    {
        await ImportSampleAsync();
        var second = await ImportSampleAsync();

        Assert.Equal(0, second.Created);
        Assert.Equal(0, second.Updated);
        Assert.Equal(88, second.Unchanged);
    }

    [Fact]
    public async Task Existing_products_are_updated_by_sku_case_insensitively()
    {
        await _database.AddProductAsync("ABC-1", "Old", 5m, 1);

        var report = await ImportAsync("name,sku,price,stock\nNew,abc-1,7.50,3\n");

        Assert.Equal(1, report.Updated);
        var product = await FindAsync("ABC-1");
        Assert.Equal("New", product!.Name);
        Assert.Equal(7.50m, product.Price);
        Assert.Equal(3, product.Stock);
    }

    [Fact]
    public async Task Missing_required_columns_fail_the_whole_import()
    {
        var report = await ImportAsync("name,description\nA,B\n");

        Assert.False(report.Succeeded);
        Assert.Contains("sku", report.FatalError);
    }

    [Fact]
    public async Task Headers_are_case_insensitive_and_order_independent()
    {
        var report = await ImportAsync(" STOCK ,Price,SKU,Name\n4,\"1,250.00\",z-1,Zed\n");

        Assert.Equal(1, report.Created);
        Assert.Equal(1250m, (await FindAsync("Z-1"))!.Price);
    }

    [Theory]
    [InlineData("10.999", "decimal places")]
    [InlineData("abc", "not a valid number")]
    [InlineData("-1", "between 0")]
    public async Task Invalid_prices_are_rejected(string price, string expected)
    {
        var report = await ImportAsync($"name,sku,price,stock\nA,A-1,{price},1\n");

        Assert.Equal(1, report.Rejected);
        Assert.Contains(report.Issues, i => i.Severity == ImportIssueSeverity.Error && i.Message.Contains(expected));
    }

    [Fact]
    public async Task Empty_file_is_reported()
    {
        var report = await ImportAsync("");

        Assert.False(report.Succeeded);
    }
}
