using System.ComponentModel.DataAnnotations;
using ECommercePlus.Domain;

namespace ECommercePlus.ViewModels;

public sealed class ProductFormModel
{
    public int Id { get; set; }

    public Guid Version { get; set; }

    [Required, StringLength(ProductRules.NameMaxLength), SafeText]
    public string Name { get; set; } = string.Empty;

    [Required, StringLength(ProductRules.SkuMaxLength), Display(Name = "SKU")]
    [RegularExpression("^[A-Za-z0-9][A-Za-z0-9_-]*$", ErrorMessage = "SKU may only contain letters, digits, '-' and '_'.")]
    public string Sku { get; set; } = string.Empty;

    [StringLength(ProductRules.DescriptionMaxLength), SafeText]
    public string? Description { get; set; }

    [StringLength(ProductRules.CategoryMaxLength), SafeText]
    public string? Category { get; set; }

    [Required, Range(typeof(decimal), "0", "1000000"), DataType(DataType.Currency)]
    public decimal? Price { get; set; }

    [Required, Range(0, ProductRules.MaxStock)]
    public int? Stock { get; set; }

    [Range(typeof(decimal), "0", "10000"), Display(Name = "Weight (kg)")]
    public decimal? WeightKg { get; set; }

    public ProductInput ToInput() =>
        ProductInput.Create(Name, Sku, Description, Category, Price ?? 0, Stock ?? 0, WeightKg);

    public static ProductFormModel From(Product product) => new()
    {
        Id = product.Id,
        Version = product.Version,
        Name = product.Name,
        Sku = product.Sku,
        Description = product.Description,
        Category = product.Category,
        Price = product.Price,
        Stock = product.Stock,
        WeightKg = product.WeightKg
    };
}
