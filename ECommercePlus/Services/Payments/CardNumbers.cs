namespace ECommercePlus.Services.Payments;

public static class CardNumbers
{
    public static string DigitsOnly(string? value) =>
        new((value ?? string.Empty).Where(char.IsAsciiDigit).ToArray());

    public static string Last4(string digits) =>
        digits.Length >= 4 ? digits[^4..] : digits;

    public static bool IsValid(string digits)
    {
        if (digits.Length is < 12 or > 19)
            return false;

        var sum = 0;
        var doubleDigit = false;
        for (var i = digits.Length - 1; i >= 0; i--)
        {
            var d = digits[i] - '0';
            if (doubleDigit)
            {
                d *= 2;
                if (d > 9)
                    d -= 9;
            }

            sum += d;
            doubleDigit = !doubleDigit;
        }

        return sum % 10 == 0;
    }
}
