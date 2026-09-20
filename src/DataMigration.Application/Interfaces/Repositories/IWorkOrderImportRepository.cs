using DataMigration.Application.Models;

namespace DataMigration.Application.Interfaces.Repositories;

public interface IWorkOrderImportRepository
{
    /// <summary>Atomically stores a batch and returns persisted results, including prior results on a resumed run.</summary>
    Task<IReadOnlyList<ImportResult>> SaveBatchAsync(Guid importRunId, IEnumerable<WorkOrderImportItem> items, CancellationToken ct);
}
