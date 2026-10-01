using ECommercePlus.Services.Import;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ECommercePlus.Data;

public sealed class SeedOptions
{
    public const string SectionName = "Seed";
    public bool Enabled { get; set; } = true;
    public string CsvPath { get; set; } = "SeedData/sample-products.csv";
}

public static class DatabaseInitializer
{
    public static async Task InitializeAsync(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        var provider = scope.ServiceProvider;
        var logger = provider.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(DatabaseInitializer));
        var db = provider.GetRequiredService<AppDbContext>();

        await db.Database.MigrateAsync(cancellationToken);

        var options = provider.GetRequiredService<IOptions<SeedOptions>>().Value;
        if (!options.Enabled || await db.Products.AnyAsync(cancellationToken))
            return;

        var environment = provider.GetRequiredService<IWebHostEnvironment>();
        var path = Path.GetFullPath(options.CsvPath, environment.ContentRootPath);
        if (!File.Exists(path))
        {
            logger.LogWarning("Seed CSV not found at {Path}; skipping seed", path);
            return;
        }

        var importer = provider.GetRequiredService<IProductCsvImporter>();
        await using var stream = File.OpenRead(path);
        var report = await importer.ImportAsync(stream, cancellationToken);
        logger.LogInformation(
            "Seeded catalog from {Path}: {Created} created, {Updated} updated, {Rejected} rejected, {Ignored} ignored",
            path, report.Created, report.Updated, report.Rejected, report.Ignored);
    }
}
