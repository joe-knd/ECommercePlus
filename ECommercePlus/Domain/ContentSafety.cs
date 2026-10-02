using System.Text.RegularExpressions;

namespace ECommercePlus.Domain;

public static partial class ContentSafety
{
    public const string MarkupMessage = "contains HTML or script content, which is not allowed";
    public const string SqlMessage = "contains a SQL-injection-like pattern, which is not allowed";
    public const string ControlMessage = "contains control characters";

    public const string MarkupPattern =
        @"<[a-z/!?]" +
        @"|(javascript|vbscript)\s*:|data\s*:\s*text/html" +
        @"|\bon(error|load|click|dblclick|mouse\w*|key\w*|focus|blur|change|submit|input|toggle|animation\w*|pointer\w*)\s*=" +
        @"|&#x?[0-9a-f]+;?|%3c";

    public const string SqlPattern =
        @"\b(drop|truncate|alter)\s+(table|database|schema|view)\b" +
        @"|\bunion\s+(all\s+)?select\b" +
        @"|\binsert\s+into\b" +
        @"|\bdelete\s+from\b" +
        @"|\bupdate\s+\w+\s+set\b" +
        @"|\b(exec|execute)\s*\(|\bxp_\w+" +
        @"|['""`]\s*\)+\s*;" +
        @"|;\s*(drop|delete|insert|update|select|truncate|alter|exec|shutdown)\b" +
        @"|['""`]\s*(or|and)\s+['""`]?\w+['""`]?\s*=" +
        @"|;\s*--|['""`]\s*--" +
        @"|/\*.*?\*/";

    private const RegexOptions Options = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;

    [GeneratedRegex(MarkupPattern, Options)]
    private static partial Regex MarkupRegex();

    [GeneratedRegex(SqlPattern, Options)]
    private static partial Regex SqlInjectionRegex();

    public static string? Check(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return null;

        if (value.Any(c => char.IsControl(c) && c is not ('\n' or '\r' or '\t')))
            return ControlMessage;

        if (MarkupRegex().IsMatch(value))
            return MarkupMessage;

        if (SqlInjectionRegex().IsMatch(value))
            return SqlMessage;

        return null;
    }
}
