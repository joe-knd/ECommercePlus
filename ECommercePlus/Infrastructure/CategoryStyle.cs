namespace ECommercePlus.Infrastructure;

public static class CategoryStyle
{
    private const int PaletteSize = 8;

    private static readonly Dictionary<string, string> Icons = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Electronics"] = "🔌",
        ["Footwear"] = "👟",
        ["Home & Office"] = "🏠",
        ["Kitchen"] = "🍳",
        ["Games"] = "🎮",
        ["Accessories"] = "👜",
        ["Stationery"] = "✏️",
        ["Outdoors"] = "🏕️",
        ["Gifts"] = "🎁",
        ["Books"] = "📚",
        ["Beauty"] = "💄",
        ["Sports"] = "⚽",
        ["Fitness"] = "🏋️",
        ["Clothing"] = "👕",
        ["Apparel"] = "👕",
        ["Toys"] = "🧸",
        ["Garden"] = "🌱",
        ["Health"] = "💊",
        ["Pets"] = "🐾",
        ["Music"] = "🎵",
        ["Travel"] = "🧳",
        ["Food"] = "🥫",
        ["Furniture"] = "🛋️",
        ["Tools"] = "🔧",
        ["Baby"] = "🍼",
        ["Automotive"] = "🚗",
        ["Misc"] = "✨"
    };

    public static string Icon(string? category) =>
        category is not null && Icons.TryGetValue(category, out var icon) ? icon : "🛍️";

    public static string HueClass(string? category)
    {
        var hash = 2166136261u;
        foreach (var c in (category ?? string.Empty).ToUpperInvariant())
            hash = unchecked((hash ^ c) * 16777619u);
        hash ^= hash >> 16;
        return $"hue-{hash % PaletteSize}";
    }
}
