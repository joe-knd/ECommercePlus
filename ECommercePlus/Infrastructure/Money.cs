using System.Globalization;

namespace ECommercePlus.Infrastructure;

public static class Money
{
    private static readonly CultureInfo Culture = CultureInfo.GetCultureInfo("en-US");

    public static string Format(decimal amount) => amount.ToString("C", Culture);
}
