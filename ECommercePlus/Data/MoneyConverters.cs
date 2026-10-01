using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace ECommercePlus.Data;

public static class MoneyConverters
{
    public static readonly ValueConverter<decimal, long> ToCents =
        new(v => (long)decimal.Round(v * 100m, 0), v => v / 100m);

    public static readonly ValueConverter<decimal?, long?> WeightToGrams =
        new(v => v.HasValue ? (long)decimal.Round(v.Value * 1000m, 0) : null, v => v.HasValue ? v.Value / 1000m : null);
}
