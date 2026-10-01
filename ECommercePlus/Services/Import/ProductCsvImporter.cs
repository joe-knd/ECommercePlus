using System.Globalization;
using System.Text;
using CsvHelper;
using CsvHelper.Configuration;
using ECommercePlus.Data;
using ECommercePlus.Domain;
using Microsoft.EntityFrameworkCore;

namespace ECommercePlus.Services.Import;

public sealed class ProductCsvImporter(AppDbContext db, ILogger<ProductCsvImporter> logger) : IProductCsvImporter
{
    public const int MaxRows = 50_000;

    private static readonly string[] RequiredColumns = ["name", "sku", "price", "stock"];
    private static readonly string[] OptionalColumns = ["description", "category", "weight_kg"];

    public async Task<ImportReport> ImportAsync(Stream csv, CancellationToken cancellationToken = default)
    {
        var report = new ImportReport();
        var accepted = new Dictionary<string, (int Row, ProductInput Input)>(StringComparer.Ordinal);

        var config = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            PrepareHeaderForMatch = args => args.Header.Trim().ToLowerInvariant(),
            TrimOptions = TrimOptions.None,
            BadDataFound = null,
            MissingFieldFound = null,
            IgnoreBlankLines = false,
            DetectDelimiter = false
        };

        using var reader = new StreamReader(csv, new UTF8Encoding(false), detectEncodingFromByteOrderMarks: true);
        using var parser = new CsvReader(reader, config);

        try
        {
            if (!await parser.ReadAsync() || !parser.ReadHeader())
                return ImportReport.Fatal("The file is empty.");
        }
        catch (Exception ex) when (ex is CsvHelperException or DecoderFallbackException)
        {
            return ImportReport.Fatal("The file could not be parsed as CSV.");
        }

        var headers = parser.HeaderRecord!.Select(h => h.Trim().ToLowerInvariant()).ToHashSet();
        var missing = RequiredColumns.Where(c => !headers.Contains(c)).ToList();
        if (missing.Count > 0)
            return ImportReport.Fatal($"Missing required column(s): {string.Join(", ", missing)}. Expected header: name,sku,description,category,price,stock,weight_kg.");

        foreach (var unknown in headers.Except(RequiredColumns).Except(OptionalColumns).Where(h => h.Length > 0))
            report.Add(1, null, ImportIssueSeverity.Warning, $"Unknown column '{unknown}' was ignored.");

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            bool hasRow;
            try
            {
                hasRow = await parser.ReadAsync();
            }
            catch (CsvHelperException)
            {
                report.Add(parser.Parser.Row, null, ImportIssueSeverity.Error, "Malformed CSV row; import stopped at this point.");
                break;
            }

            if (!hasRow)
                break;

            var row = parser.Parser.Row;
            report.TotalRows++;

            if (report.TotalRows > MaxRows)
                return ImportReport.Fatal($"The file exceeds the maximum of {MaxRows:N0} rows.");

            if (parser.Parser.Record is null || parser.Parser.Record.All(string.IsNullOrWhiteSpace))
            {
                report.Ignored++;
                report.Add(row, null, ImportIssueSeverity.Info, "Empty row skipped.");
                continue;
            }

            var input = ParseRow(parser, row, report, headers);
            if (input is null)
            {
                report.Rejected++;
                continue;
            }

            if (accepted.TryGetValue(input.Sku, out var previous))
            {
                report.Add(row, input.Sku, ImportIssueSeverity.Warning,
                    $"Duplicate SKU in file (also on row {previous.Row}); this later row takes precedence.");
                report.Ignored++;
            }

            accepted[input.Sku] = (row, input);
        }

        await PersistAsync(accepted, report, cancellationToken);
        logger.LogInformation(
            "CSV import finished: {Total} rows, {Created} created, {Updated} updated, {Unchanged} unchanged, {Rejected} rejected, {Ignored} ignored",
            report.TotalRows, report.Created, report.Updated, report.Unchanged, report.Rejected, report.Ignored);
        return report;
    }

    private static ProductInput? ParseRow(CsvReader parser, int row, ImportReport report, HashSet<string> headers)
    {
        string Field(string column) => headers.Contains(column) ? parser.GetField(column) ?? string.Empty : string.Empty;

        var rawSku = Field("sku");
        var sku = ProductRules.NormalizeSku(rawSku);
        var errors = new List<string>();

        var price = ParsePrice(Field("price"), errors, row, sku, report);
        var stock = ParseStock(Field("stock"), errors);
        var weight = ParseWeight(Field("weight_kg"), errors);

        var rawCategory = Field("category");
        var input = ProductInput.Create(Field("name"), rawSku, Field("description"), rawCategory, price ?? 0, stock ?? 0, weight);

        errors.AddRange(ProductRules.Validate(input).Select(e => e.Message));

        if (errors.Count > 0)
        {
            report.Add(row, sku, ImportIssueSeverity.Error, "Row rejected: " + string.Join(" ", errors));
            return null;
        }

        if (string.IsNullOrWhiteSpace(rawCategory))
            report.Add(row, sku, ImportIssueSeverity.Warning, $"Category is empty; assigned '{ProductRules.DefaultCategory}'.");

        if (input.Name.IndexOfAny(['<', '>']) >= 0)
            report.Add(row, sku, ImportIssueSeverity.Warning, "Name contains markup characters; it is stored as-is and always rendered as plain text.");

        if (input.Price == 0)
            report.Add(row, sku, ImportIssueSeverity.Warning, "Price is 0.00; product will be sold for free.");

        return input;
    }

    private static decimal? ParsePrice(string raw, List<string> errors, int row, string sku, ImportReport report)
    {
        var value = raw.Trim();
        if (value.Length == 0)
        {
            errors.Add("Price is required.");
            return null;
        }

        if (value.StartsWith('$'))
        {
            value = value[1..].Trim();
            report.Add(row, sku, ImportIssueSeverity.Info, $"Currency symbol removed from price '{raw.Trim()}'.");
        }

        if (!decimal.TryParse(value, NumberStyles.AllowDecimalPoint | NumberStyles.AllowThousands | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var price))
        {
            errors.Add($"Price '{raw.Trim()}' is not a valid number.");
            return null;
        }

        return price;
    }

    private static int? ParseStock(string raw, List<string> errors)
    {
        var value = raw.Trim();
        if (value.Length == 0)
        {
            errors.Add("Stock is required.");
            return null;
        }

        if (!int.TryParse(value, NumberStyles.AllowThousands | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var stock))
        {
            errors.Add($"Stock '{value}' is not a valid whole number.");
            return null;
        }

        return stock;
    }

    private static decimal? ParseWeight(string raw, List<string> errors)
    {
        var value = raw.Trim();
        if (value.Length == 0)
            return null;

        if (!decimal.TryParse(value, NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var weight))
        {
            errors.Add($"Weight '{value}' is not a valid number.");
            return null;
        }

        return weight;
    }

    private async Task PersistAsync(Dictionary<string, (int Row, ProductInput Input)> accepted, ImportReport report, CancellationToken cancellationToken)
    {
        if (accepted.Count == 0)
            return;

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        var skus = accepted.Keys.ToList();
        var existing = new Dictionary<string, Product>(StringComparer.Ordinal);
        foreach (var chunk in skus.Chunk(500))
        {
            var found = await db.Products.Where(p => chunk.Contains(p.Sku)).ToListAsync(cancellationToken);
            foreach (var product in found)
                existing[product.Sku] = product;
        }

        foreach (var (sku, (_, input)) in accepted)
        {
            if (existing.TryGetValue(sku, out var product))
            {
                if (product.Matches(input))
                {
                    report.Unchanged++;
                    continue;
                }

                product.Apply(input);
                report.Updated++;
            }
            else
            {
                var created = new Product();
                created.Apply(input);
                db.Products.Add(created);
                report.Created++;
            }
        }

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }
}
