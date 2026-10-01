using ECommercePlus.Data;
using ECommercePlus.Domain;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace ECommercePlus.Tests;

public sealed class TestDatabase : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<AppDbContext> _options;

    public TestDatabase()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        _options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options;
        using var db = CreateContext();
        db.Database.Migrate();
    }

    public AppDbContext CreateContext() => new(_options, TimeProvider.System);

    public async Task<Product> AddProductAsync(string sku, string name = "Item", decimal price = 10m, int stock = 10, string category = "General")
    {
        await using var db = CreateContext();
        var product = new Product();
        product.Apply(ProductInput.Create(name, sku, $"{name} description", category, price, stock, 1m));
        db.Products.Add(product);
        await db.SaveChangesAsync();
        return product;
    }

    public void Dispose() => _connection.Dispose();
}
