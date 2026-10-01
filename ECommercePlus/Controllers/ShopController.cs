using ECommercePlus.Services.Products;
using ECommercePlus.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace ECommercePlus.Controllers;

public class ShopController(IProductService products) : Controller
{
    public async Task<IActionResult> Index([FromQuery] ProductSearchForm search, CancellationToken cancellationToken)
    {
        var result = await products.SearchAsync(search.ToQuery(ProductSearchQuery.DefaultPageSize), cancellationToken);
        var categories = await products.GetCategoriesAsync(cancellationToken);
        return View(new ProductListViewModel(search, result, categories, nameof(Index)));
    }

    public async Task<IActionResult> Details(int id, CancellationToken cancellationToken)
    {
        var product = await products.GetAsync(id, cancellationToken);
        return product is null ? NotFound() : View(product);
    }
}
