using ECommercePlus.Domain;
using ECommercePlus.Services.Products;

namespace ECommercePlus.Services.Cart;

public sealed record CartLine(Product Product, int Quantity)
{
    public decimal LineTotal => Product.Price * Quantity;
    public bool ExceedsStock => Quantity > Product.Stock;
}

public sealed record CartView(IReadOnlyList<CartLine> Lines)
{
    public decimal Total => Lines.Sum(l => l.LineTotal);
    public int ItemCount => Lines.Sum(l => l.Quantity);
    public bool IsEmpty => Lines.Count == 0;
    public bool HasStockIssues => Lines.Any(l => l.ExceedsStock);
}

public interface ICartService
{
    int Count();
    Task<CartView> GetAsync(CancellationToken cancellationToken = default);
    Task<OperationResult> AddAsync(int productId, int quantity, CancellationToken cancellationToken = default);
    Task<OperationResult> UpdateAsync(int productId, int quantity, CancellationToken cancellationToken = default);
    void Remove(int productId);
    void Clear();
}

public sealed class CartService(ICartStore store, IProductService products) : ICartService
{
    public const int MaxQuantityPerLine = 999;

    public int Count() => store.GetLines().Values.Sum();

    public async Task<CartView> GetAsync(CancellationToken cancellationToken = default)
    {
        var lines = store.GetLines();
        if (lines.Count == 0)
            return new CartView([]);

        var found = (await products.GetManyAsync(lines.Keys, cancellationToken)).ToDictionary(p => p.Id);
        if (found.Count != lines.Count)
            store.Save(lines.Where(l => found.ContainsKey(l.Key)).ToDictionary());

        return new CartView(lines
            .Where(l => found.ContainsKey(l.Key))
            .Select(l => new CartLine(found[l.Key], l.Value))
            .OrderBy(l => l.Product.Name)
            .ToList());
    }

    public async Task<OperationResult> AddAsync(int productId, int quantity, CancellationToken cancellationToken = default)
    {
        var lines = store.GetLines();
        var current = lines.GetValueOrDefault(productId);
        return await SetAsync(productId, current + quantity, quantity, cancellationToken);
    }

    public Task<OperationResult> UpdateAsync(int productId, int quantity, CancellationToken cancellationToken = default)
    {
        if (quantity <= 0)
        {
            Remove(productId);
            return Task.FromResult(OperationResult.Success());
        }

        return SetAsync(productId, quantity, quantity, cancellationToken);
    }

    public void Remove(int productId)
    {
        var lines = store.GetLines().ToDictionary();
        if (lines.Remove(productId))
            store.Save(lines);
    }

    public void Clear() => store.Clear();

    private async Task<OperationResult> SetAsync(int productId, int newQuantity, int requested, CancellationToken cancellationToken)
    {
        if (requested <= 0)
            return OperationResult.Fail(FailureKind.Validation, "Quantity must be at least 1.");

        var product = await products.GetAsync(productId, cancellationToken);
        if (product is null)
            return OperationResult.Fail(FailureKind.NotFound, "Product not found.");

        if (newQuantity > MaxQuantityPerLine)
            return OperationResult.Fail(FailureKind.Validation, $"You can order at most {MaxQuantityPerLine} units of a product.");

        if (newQuantity > product.Stock)
            return OperationResult.Fail(FailureKind.Conflict,
                product.Stock == 0 ? $"'{product.Name}' is out of stock." : $"Only {product.Stock} unit(s) of '{product.Name}' are available.");

        var lines = store.GetLines().ToDictionary();
        lines[productId] = newQuantity;
        store.Save(lines);
        return OperationResult.Success();
    }
}
