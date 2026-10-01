namespace ECommercePlus.Domain;

public sealed record ProductInput(
    string Name,
    string Sku,
    string Description,
    string Category,
    decimal Price,
    int Stock,
    decimal? WeightKg)
{
    public static ProductInput Create(
        string? name,
        string? sku,
        string? description,
        string? category,
        decimal price,
        int stock,
        decimal? weightKg) =>
        new(
            ProductRules.NormalizeText(name),
            ProductRules.NormalizeSku(sku),
            ProductRules.NormalizeText(description),
            ProductRules.NormalizeCategory(category),
            price,
            stock,
            weightKg);
}
