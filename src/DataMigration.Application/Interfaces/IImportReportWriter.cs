using DataMigration.Application.Models;

namespace DataMigration.Application.Interfaces;

public interface IImportReportWriter
{
    Task InitializeAsync(string outputPath, CancellationToken ct);
    Task AppendAsync(IEnumerable<ImportResult> results, string outputPath, CancellationToken ct);
}
