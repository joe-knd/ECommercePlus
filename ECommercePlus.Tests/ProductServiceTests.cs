using ECommercePlus.Domain;
using ECommercePlus.Services.Products;
using Microsoft.Extensions.Logging.Abstractions;

namespace ECommercePlus.Tests;

public class ProductServiceTests : IDisposable
{
    private readonly TestDatabase _database = new();

    public void Dispose() => _database.Dispose();

    private ProductService CreateService() => new(_database.CreateContext(), NullLogger<ProductService>.Instance);

    private static ProductInput Input(string sku, string name = "Widget", decimal price = 10m, int stock = 5) =>
        ProductInput.Create(name, sku, "desc", "Tools", price, stock, 0.5m);

    [Fact]
    public async Task Create_normalizes_and_persists_product()
    {
        var result = await CreateService().CreateAsync(ProductInput.Create("  Big   Widget ", " ab-1 ", null, " ", 3.5m, 2, null));

        Assert.True(result.Succeeded);
        var product = await CreateService().GetAsync(result.Value!.Id);
        Assert.Equal("Big Widget", product!.Name);
        Assert.Equal("AB-1", product.Sku);
        Assert.Equal(ProductRules.DefaultCategory, product.Category);
    }

    [Fact]
    public async Task Create_rejects_duplicate_sku()
    {
        await _database.AddProductAsync("DUP-1");

        var result = await CreateService().CreateAsync(Input("dup-1"));

        Assert.Equal(FailureKind.Conflict, result.Failure);
    }

    [Fact]
    public async Task Create_rejects_invalid_input()
    {
        var result = await CreateService().CreateAsync(ProductInput.Create("", "bad sku!", null, null, -1m, -1, -2m));

        Assert.Equal(FailureKind.Validation, result.Failure);
        Assert.Equal(
            [nameof(ProductInput.Name), nameof(ProductInput.Sku), nameof(ProductInput.Price), nameof(ProductInput.Stock), nameof(ProductInput.WeightKg)],
            result.Errors.Select(e => e.Field).ToArray());
    }

    [Fact]
    public async Task Update_detects_concurrent_modification()
    {
        var product = await _database.AddProductAsync("V-1");

        var first = await CreateService().UpdateAsync(product.Id, Input("V-1", "First"), product.Version);
        var second = await CreateService().UpdateAsync(product.Id, Input("V-1", "Second"), product.Version);

        Assert.True(first.Succeeded);
        Assert.Equal(FailureKind.Conflict, second.Failure);
        Assert.Equal("First", (await CreateService().GetAsync(product.Id))!.Name);
    }

    [Fact]
    public async Task Delete_removes_product()
    {
        var product = await _database.AddProductAsync("DEL-1");

        Assert.True((await CreateService().DeleteAsync(product.Id)).Succeeded);
        Assert.Null(await CreateService().GetAsync(product.Id));
        Assert.Equal(FailureKind.NotFound, (await CreateService().DeleteAsync(product.Id)).Failure);
    }

    [Fact]
    public async Task Search_filters_by_text_category_price_and_stock()
    {
        await _database.AddProductAsync("A-1", "Red Shoe", 50m, 3, "Footwear");
        await _database.AddProductAsync("A-2", "Blue Shoe", 80m, 0, "Footwear");
        await _database.AddProductAsync("A-3", "Red Hat", 20m, 9, "Accessories");

        var service = CreateService();

        Assert.Equal(2, (await service.SearchAsync(new ProductSearchQuery { Text = "shoe" })).TotalCount);
        Assert.Equal(2, (await service.SearchAsync(new ProductSearchQuery { Text = "RED" })).TotalCount);
        Assert.Equal(1, (await service.SearchAsync(new ProductSearchQuery { Text = "a-3" })).TotalCount);
        Assert.Equal(2, (await service.SearchAsync(new ProductSearchQuery { Category = "Footwear" })).TotalCount);
        Assert.Equal(1, (await service.SearchAsync(new ProductSearchQuery { Category = "Footwear", InStockOnly = true })).TotalCount);
        Assert.Equal(2, (await service.SearchAsync(new ProductSearchQuery { MinPrice = 30m, MaxPrice = 100m })).TotalCount);

        var sorted = await service.SearchAsync(new ProductSearchQuery { Sort = ProductSort.PriceDesc });
        Assert.Equal(["A-2", "A-1", "A-3"], sorted.Items.Select(p => p.Sku).ToArray());
    }

    [Fact]
    public async Task Search_treats_wildcards_literally()
    {
        await _database.AddProductAsync("W-1", "100% Cotton");
        await _database.AddProductAsync("W-2", "Plain Cotton");

        var result = await CreateService().SearchAsync(new ProductSearchQuery { Text = "100%" });

        Assert.Single(result.Items);
    }

    [Fact]
    public async Task Search_paginates()
    {
        for (var i = 0; i < 5; i++)
            await _database.AddProductAsync($"P-{i}", $"Item {i}");

        var page = await CreateService().SearchAsync(new ProductSearchQuery { Page = 2, PageSize = 2 });

        Assert.Equal(5, page.TotalCount);
        Assert.Equal(3, page.TotalPages);
        Assert.Equal(["P-2", "P-3"], page.Items.Select(p => p.Sku).ToArray());
    }
}
