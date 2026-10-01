namespace ECommercePlus.Services.Products;

public enum ProductSort
{
    Name,
    PriceAsc,
    PriceDesc,
    Newest,
    StockAsc
}

public sealed record ProductSearchQuery
{
    public const int DefaultPageSize = 12;
    public const int MaxPageSize = 100;

    public string? Text { get; init; }
    public string? Category { get; init; }
    public decimal? MinPrice { get; init; }
    public decimal? MaxPrice { get; init; }
    public bool InStockOnly { get; init; }
    public ProductSort Sort { get; init; } = ProductSort.Name;
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = DefaultPageSize;
}

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount)
{
    public int TotalPages => TotalCount == 0 ? 1 : (int)Math.Ceiling(TotalCount / (double)PageSize);
    public bool HasPrevious => Page > 1;
    public bool HasNext => Page < TotalPages;
}
