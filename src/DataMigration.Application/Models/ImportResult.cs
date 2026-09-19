namespace DataMigration.Application.Models;

/// <summary>A single Excel row's outcome, used to produce the import report.</summary>
public sealed record ImportResult(
    int RowIndex,
    bool Successful,
    IReadOnlyList<ImportError> Errors,
    string Technician,
    string Client,
    string Total,
    string Information);
