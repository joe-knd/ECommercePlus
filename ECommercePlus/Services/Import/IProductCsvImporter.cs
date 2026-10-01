namespace ECommercePlus.Services.Import;

public interface IProductCsvImporter
{
    Task<ImportReport> ImportAsync(Stream csv, CancellationToken cancellationToken = default);
}
