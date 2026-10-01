using ECommercePlus.Domain;
using ECommercePlus.Infrastructure;
using ECommercePlus.Services.Import;
using ECommercePlus.Services.Products;
using ECommercePlus.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace ECommercePlus.Controllers;

public class ProductsController(IProductService products, IProductCsvImporter importer) : Controller
{
    public const long MaxImportBytes = 5 * 1024 * 1024;
    private const int PageSize = 20;

    public async Task<IActionResult> Index([FromQuery] ProductSearchForm search, CancellationToken cancellationToken)
    {
        var result = await products.SearchAsync(search.ToQuery(PageSize), cancellationToken);
        var categories = await products.GetCategoriesAsync(cancellationToken);
        return View(new ProductListViewModel(search, result, categories, nameof(Index)));
    }

    public async Task<IActionResult> Details(int id, CancellationToken cancellationToken)
    {
        var product = await products.GetAsync(id, cancellationToken);
        return product is null ? NotFound() : View(product);
    }

    public async Task<IActionResult> Create(CancellationToken cancellationToken)
    {
        await LoadCategoriesAsync(cancellationToken);
        return View(new ProductFormModel());
    }

    [HttpPost]
    public async Task<IActionResult> Create(ProductFormModel form, CancellationToken cancellationToken)
    {
        if (ModelState.IsValid)
        {
            var result = await products.CreateAsync(form.ToInput(), cancellationToken);
            if (result.Succeeded)
            {
                TempData[TempDataKeys.Success] = $"Product '{result.Value!.Name}' created.";
                return RedirectToAction(nameof(Details), new { id = result.Value.Id });
            }

            ModelState.AddErrors(result);
        }

        await LoadCategoriesAsync(cancellationToken);
        return View(form);
    }

    public async Task<IActionResult> Edit(int id, CancellationToken cancellationToken)
    {
        var product = await products.GetAsync(id, cancellationToken);
        if (product is null)
            return NotFound();

        await LoadCategoriesAsync(cancellationToken);
        return View(ProductFormModel.From(product));
    }

    [HttpPost]
    public async Task<IActionResult> Edit(int id, ProductFormModel form, CancellationToken cancellationToken)
    {
        if (ModelState.IsValid)
        {
            var result = await products.UpdateAsync(id, form.ToInput(), form.Version, cancellationToken);
            if (result.Succeeded)
            {
                TempData[TempDataKeys.Success] = $"Product '{result.Value!.Name}' updated.";
                return RedirectToAction(nameof(Details), new { id });
            }

            if (result.Failure == FailureKind.NotFound)
                return NotFound();

            ModelState.AddErrors(result);
        }

        form.Id = id;
        await LoadCategoriesAsync(cancellationToken);
        return View(form);
    }

    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var product = await products.GetAsync(id, cancellationToken);
        return product is null ? NotFound() : View(product);
    }

    [HttpPost, ActionName(nameof(Delete))]
    public async Task<IActionResult> DeleteConfirmed(int id, CancellationToken cancellationToken)
    {
        var result = await products.DeleteAsync(id, cancellationToken);
        if (result.Failure == FailureKind.NotFound)
            return NotFound();

        TempData[TempDataKeys.Success] = "Product deleted.";
        return RedirectToAction(nameof(Index));
    }

    public IActionResult Import() => View();

    [HttpPost]
    [RequestSizeLimit(MaxImportBytes + 64 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaxImportBytes + 64 * 1024)]
    public async Task<IActionResult> Import(IFormFile? file, CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
            ModelState.AddModelError("file", "Please choose a non-empty CSV file.");
        else if (file.Length > MaxImportBytes)
            ModelState.AddModelError("file", $"The file must be smaller than {MaxImportBytes / 1024 / 1024} MB.");
        else if (!string.Equals(Path.GetExtension(file.FileName), ".csv", StringComparison.OrdinalIgnoreCase))
            ModelState.AddModelError("file", "Only .csv files are supported.");

        if (!ModelState.IsValid)
            return View();

        await using var stream = file!.OpenReadStream();
        var report = await importer.ImportAsync(stream, cancellationToken);
        return View("ImportResult", report);
    }

    private async Task LoadCategoriesAsync(CancellationToken cancellationToken) =>
        ViewBag.Categories = await products.GetCategoriesAsync(cancellationToken);
}
