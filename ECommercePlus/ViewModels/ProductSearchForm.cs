using ECommercePlus.Services.Products;

namespace ECommercePlus.ViewModels;

public sealed class ProductSearchForm
{
    public string? Q { get; set; }

    public string? Category { get; set; }

    public decimal? MinPrice { get; set; }

    public decimal? MaxPrice { get; set; }

    public bool InStock { get; set; }

    public ProductSort Sort { get; set; } = ProductSort.Name;

    public int Page { get; set; } = 1;

    public ProductSearchQuery ToQuery(int pageSize) => new()
    {
        Text = Q,
        Category = Category,
        MinPrice = MinPrice is < 0 ? null : MinPrice,
        MaxPrice = MaxPrice is < 0 ? null : MaxPrice,
        InStockOnly = InStock,
        Sort = Sort,
        Page = Page,
        PageSize = pageSize
    };

    public Dictionary<string, string?> ToRouteValues(int page)
    {
        var values = new Dictionary<string, string?>();
        if (!string.IsNullOrWhiteSpace(Q)) values["q"] = Q;
        if (!string.IsNullOrWhiteSpace(Category)) values["category"] = Category;
        if (MinPrice is not null) values["minPrice"] = MinPrice.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
        if (MaxPrice is not null) values["maxPrice"] = MaxPrice.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
        if (InStock) values["inStock"] = "true";
        if (Sort != ProductSort.Name) values["sort"] = Sort.ToString();
        values["page"] = page.ToString();
        return values;
    }
}

public sealed record ProductListViewModel(
    ProductSearchForm Search,
    PagedResult<Domain.Product> Result,
    IReadOnlyList<string> Categories,
    string Action);
