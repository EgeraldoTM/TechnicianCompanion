using DataMigration.Core.Models;

namespace DataMigration.Application.Interfaces.Repositories;

public interface IWorkOrderRepository
{
    Task BulkInsertAsync(IEnumerable<WorkOrder> workOrders, CancellationToken ct);
}
