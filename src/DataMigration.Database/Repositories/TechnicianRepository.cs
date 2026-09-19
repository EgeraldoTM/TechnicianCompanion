using System.Data;
using DataMigration.Application.Interfaces.Repositories;
using DataMigration.Core.Models;
using Microsoft.Data.SqlClient;

namespace DataMigration.Database.Repositories;

public sealed class TechnicianRepository(string connectionString) : ITechnicianRepository
{
    private const string TableName = "Technicians";
    private const string TempTableName = "#TechniciansStaging";
    private readonly string _connectionString = connectionString;

    public async Task<IReadOnlyList<Technician>> UpsertTechniciansAsync(
        IEnumerable<Technician> technicians,
        CancellationToken ct)
    {
        await using SqlConnection connection = new(_connectionString);
        await connection.OpenAsync(ct);

        await PopulateTempTableAsync(connection, technicians, ct);
        await UpsertAsync(connection, ct);

        List<Technician> result = [];
        await using SqlCommand selectCommand = new($"""
            SELECT t.Id, t.FirstName, t.LastName
            FROM {TableName} t
            INNER JOIN {TempTableName} s
                ON t.FirstName = s.FirstName AND t.LastName = s.LastName;
        """, connection);
        await using SqlDataReader reader = await selectCommand.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            result.Add(new Technician(reader.GetString(1), reader.GetString(2)) { Id = reader.GetInt32(0) });

        return result;
    }

    private static async Task PopulateTempTableAsync(
        SqlConnection connection,
        IEnumerable<Technician> technicians,
        CancellationToken ct)
    {
        await using (SqlCommand command = new(
            $"CREATE TABLE {TempTableName} (FirstName NVARCHAR(100), LastName NVARCHAR(100));", connection))
        {
            await command.ExecuteNonQueryAsync(ct);
        }

        DataTable table = new();
        table.Columns.Add(nameof(Technician.FirstName), typeof(string));
        table.Columns.Add(nameof(Technician.LastName), typeof(string));
        foreach (Technician technician in technicians)
            table.Rows.Add(technician.FirstName, technician.LastName);

        using SqlBulkCopy bulkCopy = new(connection) { DestinationTableName = TempTableName };
        bulkCopy.ColumnMappings.Add(nameof(Technician.FirstName), nameof(Technician.FirstName));
        bulkCopy.ColumnMappings.Add(nameof(Technician.LastName), nameof(Technician.LastName));
        await bulkCopy.WriteToServerAsync(table, ct);
    }

    private static async Task UpsertAsync(SqlConnection connection, CancellationToken ct)
    {
        await using SqlCommand command = new($"""
            INSERT INTO {TableName} (FirstName, LastName)
            SELECT s.FirstName, s.LastName
            FROM {TempTableName} s
            WHERE NOT EXISTS (
                SELECT 1 FROM {TableName} t
                WHERE t.FirstName = s.FirstName AND t.LastName = s.LastName
            );
        """, connection);
        await command.ExecuteNonQueryAsync(ct);
    }
}
