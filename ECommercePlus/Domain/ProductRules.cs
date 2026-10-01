using System.Text.RegularExpressions;

namespace ECommercePlus.Domain;

public static partial class ProductRules
{
    public const int NameMaxLength = 200;
    public const int SkuMaxLength = 50;
    public const int DescriptionMaxLength = 2000;
    public const int CategoryMaxLength = 50;
    public const decimal MaxPrice = 1_000_000m;
    public const int MaxStock = 1_000_000;
    public const decimal MaxWeightKg = 10_000m;
    public const int PriceDecimals = 2;
    public const int WeightDecimals = 3;
    public const string DefaultCategory = "Uncategorized";
    public const string SkuPattern = "^[A-Z0-9][A-Z0-9_-]*$";

    [GeneratedRegex(SkuPattern)]
    private static partial Regex SkuRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();

    public static string NormalizeText(string? value) =>
        string.IsNullOrWhiteSpace(value) ? string.Empty : WhitespaceRegex().Replace(value.Trim(), " ");

    public static string NormalizeSku(string? value) =>
        string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim().ToUpperInvariant();

    public static string NormalizeCategory(string? value)
    {
        var normalized = NormalizeText(value);
        return normalized.Length == 0 ? DefaultCategory : normalized;
    }

    public static IReadOnlyList<ValidationError> Validate(ProductInput input)
    {
        var errors = new List<ValidationError>();

        if (input.Name.Length == 0)
            errors.Add(new(nameof(ProductInput.Name), "Name is required."));
        else if (input.Name.Length > NameMaxLength)
            errors.Add(new(nameof(ProductInput.Name), $"Name must be at most {NameMaxLength} characters."));

        if (input.Sku.Length == 0)
            errors.Add(new(nameof(ProductInput.Sku), "SKU is required."));
        else if (input.Sku.Length > SkuMaxLength)
            errors.Add(new(nameof(ProductInput.Sku), $"SKU must be at most {SkuMaxLength} characters."));
        else if (!SkuRegex().IsMatch(input.Sku))
            errors.Add(new(nameof(ProductInput.Sku), "SKU may only contain letters, digits, '-' and '_'."));

        if (input.Description.Length > DescriptionMaxLength)
            errors.Add(new(nameof(ProductInput.Description), $"Description must be at most {DescriptionMaxLength} characters."));

        if (input.Category.Length > CategoryMaxLength)
            errors.Add(new(nameof(ProductInput.Category), $"Category must be at most {CategoryMaxLength} characters."));

        if (input.Price < 0 || input.Price > MaxPrice)
            errors.Add(new(nameof(ProductInput.Price), $"Price must be between 0 and {MaxPrice:N0}."));
        else if (decimal.Round(input.Price, PriceDecimals) != input.Price)
            errors.Add(new(nameof(ProductInput.Price), $"Price can have at most {PriceDecimals} decimal places."));

        if (input.Stock < 0 || input.Stock > MaxStock)
            errors.Add(new(nameof(ProductInput.Stock), $"Stock must be between 0 and {MaxStock:N0}."));

        if (input.WeightKg is { } weight)
        {
            if (weight < 0 || weight > MaxWeightKg)
                errors.Add(new(nameof(ProductInput.WeightKg), $"Weight must be between 0 and {MaxWeightKg:N0} kg."));
            else if (decimal.Round(weight, WeightDecimals) != weight)
                errors.Add(new(nameof(ProductInput.WeightKg), $"Weight can have at most {WeightDecimals} decimal places."));
        }

        return errors;
    }
}
