using ECommercePlus.Data;
using ECommercePlus.Domain;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace ECommercePlus.Services.Products;

public sealed class ProductService(AppDbContext db, ILogger<ProductService> logger) : IProductService
{
    private const char LikeEscape = '\\';

    public async Task<PagedResult<Product>> SearchAsync(ProductSearchQuery query, CancellationToken cancellationToken = default)
    {
        var pageSize = Math.Clamp(query.PageSize, 1, ProductSearchQuery.MaxPageSize);
        var page = Math.Max(1, query.Page);

        var products = db.Products.AsNoTracking();

        var text = ProductRules.NormalizeText(query.Text);
        if (text.Length > 0)
        {
            var pattern = $"%{EscapeLike(text)}%";
            products = products.Where(p =>
                EF.Functions.Like(p.Name, pattern, LikeEscape.ToString()) ||
                EF.Functions.Like(p.Sku, pattern, LikeEscape.ToString()) ||
                EF.Functions.Like(p.Description, pattern, LikeEscape.ToString()) ||
                EF.Functions.Like(p.Category, pattern, LikeEscape.ToString()));
        }

        var category = ProductRules.NormalizeText(query.Category);
        if (category.Length > 0)
            products = products.Where(p => p.Category == category);

        if (query.MinPrice is { } min)
            products = products.Where(p => p.Price >= min);

        if (query.MaxPrice is { } max)
            products = products.Where(p => p.Price <= max);

        if (query.InStockOnly)
            products = products.Where(p => p.Stock > 0);

        products = query.Sort switch
        {
            ProductSort.PriceAsc => products.OrderBy(p => p.Price).ThenBy(p => p.Name),
            ProductSort.PriceDesc => products.OrderByDescending(p => p.Price).ThenBy(p => p.Name),
            ProductSort.Newest => products.OrderByDescending(p => p.CreatedAtUtc).ThenByDescending(p => p.Id),
            ProductSort.StockAsc => products.OrderBy(p => p.Stock).ThenBy(p => p.Name),
            _ => products.OrderBy(p => p.Name).ThenBy(p => p.Sku)
        };

        var total = await products.CountAsync(cancellationToken);
        var items = await products.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
        return new PagedResult<Product>(items, page, pageSize, total);
    }

    public Task<Product?> GetAsync(int id, CancellationToken cancellationToken = default) =>
        db.Products.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Product>> GetManyAsync(IEnumerable<int> ids, CancellationToken cancellationToken = default)
    {
        var idList = ids.Distinct().ToList();
        return await db.Products.AsNoTracking().Where(p => idList.Contains(p.Id)).ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<string>> GetCategoriesAsync(CancellationToken cancellationToken = default) =>
        await db.Products.AsNoTracking().Select(p => p.Category).Distinct().OrderBy(c => c).ToListAsync(cancellationToken);

    public async Task<OperationResult<Product>> CreateAsync(ProductInput input, CancellationToken cancellationToken = default)
    {
        var errors = ProductRules.Validate(input);
        if (errors.Count > 0)
            return OperationResult<Product>.Invalid(errors);

        if (await db.Products.AnyAsync(p => p.Sku == input.Sku, cancellationToken))
            return DuplicateSku(input.Sku);

        var product = new Product();
        product.Apply(input);
        db.Products.Add(product);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            return DuplicateSku(input.Sku);
        }

        logger.LogInformation("Product {ProductId} ({Sku}) created", product.Id, product.Sku);
        return OperationResult<Product>.Success(product);
    }

    public async Task<OperationResult<Product>> UpdateAsync(int id, ProductInput input, Guid expectedVersion, CancellationToken cancellationToken = default)
    {
        var errors = ProductRules.Validate(input);
        if (errors.Count > 0)
            return OperationResult<Product>.Invalid(errors);

        var product = await db.Products.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
        if (product is null)
            return OperationResult<Product>.Fail(FailureKind.NotFound, "Product not found.");

        if (product.Version != expectedVersion)
            return ConcurrencyConflict();

        if (product.Sku != input.Sku && await db.Products.AnyAsync(p => p.Sku == input.Sku && p.Id != id, cancellationToken))
            return DuplicateSku(input.Sku);

        product.Apply(input);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return ConcurrencyConflict();
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            return DuplicateSku(input.Sku);
        }

        logger.LogInformation("Product {ProductId} ({Sku}) updated", product.Id, product.Sku);
        return OperationResult<Product>.Success(product);
    }

    public async Task<OperationResult> DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var deleted = await db.Products.Where(p => p.Id == id).ExecuteDeleteAsync(cancellationToken);
        if (deleted == 0)
            return OperationResult.Fail(FailureKind.NotFound, "Product not found.");

        logger.LogInformation("Product {ProductId} deleted", id);
        return OperationResult.Success();
    }

    private static OperationResult<Product> DuplicateSku(string sku) =>
        OperationResult<Product>.Fail(FailureKind.Conflict, $"A product with SKU '{sku}' already exists.", nameof(ProductInput.Sku));

    private static OperationResult<Product> ConcurrencyConflict() =>
        OperationResult<Product>.Fail(FailureKind.Conflict, "This product was modified by someone else. Reload the page and try again.");

    private static bool IsUniqueViolation(DbUpdateException ex) =>
        ex.InnerException is SqliteException { SqliteErrorCode: 19 };

    internal static string EscapeLike(string value) =>
        value.Replace(@"\", @"\\").Replace("%", @"\%").Replace("_", @"\_");
}
