namespace ECommercePlus.Domain;

public class Product
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Category { get; set; } = ProductRules.DefaultCategory;
    public decimal Price { get; set; }
    public int Stock { get; set; }
    public decimal? WeightKg { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public Guid Version { get; set; } = Guid.NewGuid();

    public bool InStock => Stock > 0;

    public void Apply(ProductInput input)
    {
        Name = input.Name;
        Sku = input.Sku;
        Description = input.Description;
        Category = input.Category;
        Price = input.Price;
        Stock = input.Stock;
        WeightKg = input.WeightKg;
    }

    public bool Matches(ProductInput input) =>
        Name == input.Name &&
        Sku == input.Sku &&
        Description == input.Description &&
        Category == input.Category &&
        Price == input.Price &&
        Stock == input.Stock &&
        WeightKg == input.WeightKg;
}
