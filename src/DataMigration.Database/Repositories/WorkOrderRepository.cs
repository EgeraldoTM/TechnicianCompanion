using System.Data;
using DataMigration.Application.Interfaces.Repositories;
using DataMigration.Core.Models;
using Microsoft.Data.SqlClient;

namespace DataMigration.Database.Repositories;

public sealed class WorkOrderRepository(string connectionString) : IWorkOrderRepository
{
    private readonly string _connectionString = connectionString;

    public async Task BulkInsertAsync(IEnumerable<WorkOrder> workOrders, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(workOrders);

        DataTable table = new();
        table.Columns.Add(nameof(WorkOrder.TechnicianId), typeof(int));
        table.Columns.Add(nameof(WorkOrder.ClientId), typeof(int));
        table.Columns.Add(nameof(WorkOrder.Information), typeof(string));
        table.Columns.Add(nameof(WorkOrder.Date), typeof(DateTime));
        table.Columns.Add(nameof(WorkOrder.Total), typeof(decimal));

        foreach (WorkOrder workOrder in workOrders)
            table.Rows.Add(workOrder.TechnicianId, workOrder.ClientId, workOrder.Information,
                workOrder.Date.ToDateTime(TimeOnly.MinValue), workOrder.Total);

        if (table.Rows.Count == 0) return;

        await using SqlConnection connection = new(_connectionString);
        await connection.OpenAsync(ct);

        using SqlBulkCopy bulkCopy = new(connection) { DestinationTableName = "WorkOrders" };
        bulkCopy.ColumnMappings.Add(nameof(WorkOrder.TechnicianId), nameof(WorkOrder.TechnicianId));
        bulkCopy.ColumnMappings.Add(nameof(WorkOrder.ClientId), nameof(WorkOrder.ClientId));
        bulkCopy.ColumnMappings.Add(nameof(WorkOrder.Information), nameof(WorkOrder.Information));
        bulkCopy.ColumnMappings.Add(nameof(WorkOrder.Date), nameof(WorkOrder.Date));
        bulkCopy.ColumnMappings.Add(nameof(WorkOrder.Total), nameof(WorkOrder.Total));

        await bulkCopy.WriteToServerAsync(table, ct);
    }
}
