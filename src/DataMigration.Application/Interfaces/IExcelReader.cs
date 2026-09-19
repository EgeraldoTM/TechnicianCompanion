using DataMigration.Application.Models;

namespace DataMigration.Application.Interfaces;

public interface IExcelReader
{
    IAsyncEnumerable<WorkOrderExcelRow> ReadWorkOrdersAsync(string filePath, CancellationToken ct);
    IAsyncEnumerable<string> ReadClientsAsync(string filePath, CancellationToken ct);
    IAsyncEnumerable<string> ReadTechniciansAsync(string filePath, CancellationToken ct);
}
