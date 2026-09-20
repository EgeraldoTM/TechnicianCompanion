using DataMigration.Application.Models;

namespace DataMigration.Application.Interfaces.Repositories;

public interface IImportRunRepository
{
    Task<ImportRun> GetOrCreateAsync(string sourceFileHash, string sourceFileName, CancellationToken ct);
    Task CompleteAsync(Guid importRunId, bool completedWithErrors, CancellationToken ct);
}
