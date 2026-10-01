namespace ECommercePlus.Services.Import;

public enum ImportIssueSeverity
{
    Info,
    Warning,
    Error
}

public sealed record ImportIssue(int Row, string? Sku, ImportIssueSeverity Severity, string Message);

public sealed class ImportReport
{
    private readonly List<ImportIssue> _issues = [];

    public int TotalRows { get; internal set; }
    public int Created { get; internal set; }
    public int Updated { get; internal set; }
    public int Unchanged { get; internal set; }
    public int Rejected { get; internal set; }
    public int Ignored { get; internal set; }
    public string? FatalError { get; internal set; }
    public bool Succeeded => FatalError is null;
    public IReadOnlyList<ImportIssue> Issues => _issues;

    internal void Add(int row, string? sku, ImportIssueSeverity severity, string message) =>
        _issues.Add(new ImportIssue(row, string.IsNullOrEmpty(sku) ? null : sku, severity, message));

    internal static ImportReport Fatal(string message) => new() { FatalError = message };
}
