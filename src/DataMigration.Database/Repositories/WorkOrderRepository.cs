using System.Data;
using DataMigration.Application.Interfaces.Repositories;
using DataMigration.Application.Models;
using Microsoft.Data.SqlClient;

namespace DataMigration.Database.Repositories;

public sealed class WorkOrderRepository(string connectionString) : IWorkOrderImportRepository
{
    private readonly string _connectionString = connectionString;

    public async Task<IReadOnlyList<ImportResult>> SaveBatchAsync(Guid importRunId, IEnumerable<WorkOrderImportItem> items, CancellationToken ct)
    {
        List<WorkOrderImportItem> batch = [.. items];
        if (batch.Count == 0) return [];
        await using SqlConnection connection = new(_connectionString);
        await connection.OpenAsync(ct);
        await using SqlTransaction transaction = (SqlTransaction)await connection.BeginTransactionAsync(ct);
        try
        {
            await CreateStageAsync(connection, transaction, ct);
            await StageAsync(connection, transaction, batch, ct);
            await ExecuteAsync("INSERT INTO WorkOrders (TechnicianId, ClientId, Information, [Date], Total, ImportRunId, SourceRowIndex) SELECT s.TechnicianId, s.ClientId, s.Information, s.WorkOrderDate, s.WorkOrderTotal, @runId, s.RowIndex FROM #ImportRows s WHERE s.Successful = 1 AND NOT EXISTS (SELECT 1 FROM ImportRowResults r WHERE r.ImportRunId = @runId AND r.SourceRowIndex = s.RowIndex);", connection, transaction, importRunId, ct);
            await ExecuteAsync("INSERT INTO ImportRowResults (ImportRunId, SourceRowIndex, Successful, Errors, Technician, Client, Total, Information) SELECT @runId, s.RowIndex, s.Successful, s.Errors, s.Technician, s.Client, s.TotalText, s.Information FROM #ImportRows s WHERE NOT EXISTS (SELECT 1 FROM ImportRowResults r WHERE r.ImportRunId = @runId AND r.SourceRowIndex = s.RowIndex);", connection, transaction, importRunId, ct);
            List<ImportResult> result = await ReadAsync(connection, transaction, importRunId, ct);
            await transaction.CommitAsync(ct);
            return result;
        }
        catch { await transaction.RollbackAsync(CancellationToken.None); throw; }
    }

    private static async Task CreateStageAsync(SqlConnection c, SqlTransaction t, CancellationToken ct) =>
        await ExecuteAsync("CREATE TABLE #ImportRows (RowIndex INT NOT NULL, Successful BIT NOT NULL, Errors NVARCHAR(MAX) NOT NULL, Technician NVARCHAR(200) NOT NULL, Client NVARCHAR(200) NOT NULL, TotalText NVARCHAR(100) NOT NULL, Information NVARCHAR(MAX) NOT NULL, TechnicianId INT NULL, ClientId INT NULL, WorkOrderDate DATE NULL, WorkOrderTotal DECIMAL(18,2) NULL);", c, t, null, ct);

    private static async Task StageAsync(SqlConnection c, SqlTransaction t, IEnumerable<WorkOrderImportItem> items, CancellationToken ct)
    {
        DataTable table = new();
        string[] names = ["RowIndex", "Successful", "Errors", "Technician", "Client", "TotalText", "Information", "TechnicianId", "ClientId", "WorkOrderDate", "WorkOrderTotal"];
        foreach (string name in names) table.Columns.Add(name, typeof(object));
        foreach (WorkOrderImportItem item in items) table.Rows.Add(item.Result.RowIndex, item.Result.Successful, string.Join('\n', item.Result.Errors.Select(e => e.Message)), item.Result.Technician, item.Result.Client, item.Result.Total, item.Result.Information, item.WorkOrder?.TechnicianId ?? (object)DBNull.Value, item.WorkOrder?.ClientId ?? (object)DBNull.Value, item.WorkOrder?.Date.ToDateTime(TimeOnly.MinValue) ?? (object)DBNull.Value, item.WorkOrder?.Total ?? (object)DBNull.Value);
        using SqlBulkCopy copy = new(c, SqlBulkCopyOptions.Default, t) { DestinationTableName = "#ImportRows" };
        foreach (DataColumn column in table.Columns) copy.ColumnMappings.Add(column.ColumnName, column.ColumnName);
        await copy.WriteToServerAsync(table, ct);
    }

    private static async Task ExecuteAsync(string sql, SqlConnection c, SqlTransaction t, Guid? runId, CancellationToken ct)
    {
        await using SqlCommand command = new(sql, c, t);
        if (runId.HasValue) command.Parameters.AddWithValue("@runId", runId.Value);
        await command.ExecuteNonQueryAsync(ct);
    }

    private static async Task<List<ImportResult>> ReadAsync(SqlConnection c, SqlTransaction t, Guid runId, CancellationToken ct)
    {
        await using SqlCommand command = new("SELECT r.SourceRowIndex, r.Successful, r.Errors, r.Technician, r.Client, r.Total, r.Information FROM ImportRowResults r JOIN #ImportRows s ON s.RowIndex = r.SourceRowIndex WHERE r.ImportRunId = @runId ORDER BY r.SourceRowIndex;", c, t);
        command.Parameters.AddWithValue("@runId", runId);
        await using SqlDataReader reader = await command.ExecuteReaderAsync(ct);
        List<ImportResult> results = [];
        while (await reader.ReadAsync(ct)) { string errors = reader.GetString(2); results.Add(new ImportResult(reader.GetInt32(0), reader.GetBoolean(1), errors.Length == 0 ? [] : errors.Split('\n').Select(message => new ImportError("persisted", message)).ToList(), reader.GetString(3), reader.GetString(4), reader.GetString(5), reader.GetString(6))); }
        return results;
    }
}
