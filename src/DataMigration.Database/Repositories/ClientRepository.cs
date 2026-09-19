using System.Data;
using DataMigration.Application.Interfaces.Repositories;
using DataMigration.Core.Models;
using Microsoft.Data.SqlClient;

namespace DataMigration.Database.Repositories;

public class ClientRepository(string connectionString) : IClientRepository
{
    private const string TableName = "Clients";
    private const string TempTableName = "#ClientsStaging";
    private readonly string _connectionString = connectionString;
    
    public async Task<IReadOnlyList<Client>> UpsertClientsAsync(IEnumerable<Client> clients, CancellationToken ct)
    {
        await using SqlConnection connection = new(_connectionString);
        await connection.OpenAsync(ct);

        await PopulateTempTableAsync(connection, clients, ct);
        await UpsertClientsAsync(connection, ct);

        // Return the full set (pre-existing + newly inserted) for every staged name,
        // so the caller gets IDs for all of them regardless of which were new.
        List<(int Id, string FirstName, string LastName)> result = [];
        await using (SqlCommand selectCmd = new($"""
            SELECT t.{nameof(Client.Id)}, t.{nameof(Client.FirstName)}, t.{nameof(Client.LastName)}
            FROM {TableName} t
            INNER JOIN {TempTableName} s
                ON t.FirstName = s.FirstName AND t.LastName = s.LastName;
        """, connection))
            
        await using (var rd = await selectCmd.ExecuteReaderAsync(ct))
        {
            while (await rd.ReadAsync(ct))
                result.Add((rd.GetInt32(0), rd.GetString(1), rd.GetString(2)));
        }

        return result.Select(r => new Client(r.FirstName, r.LastName) { Id = r.Id }).ToList();
    }

    private async Task PopulateTempTableAsync(SqlConnection connection, IEnumerable<Client> clients, CancellationToken ct)
    {
        const string sql = $"CREATE TABLE {TempTableName} (FirstName NVARCHAR(100), LastName NVARCHAR(100));";
        await using (SqlCommand createTemp = new(sql, connection))
        {
            await createTemp.ExecuteNonQueryAsync(ct);
        }
        
        DataTable table = new();
        table.Columns.Add(nameof(Client.FirstName), typeof(string));
        table.Columns.Add(nameof(Client.LastName), typeof(string));
        foreach (var client in clients)
            table.Rows.Add(client.FirstName, client.LastName);

        using (SqlBulkCopy bulkCopy = new(connection))
        {
            bulkCopy.DestinationTableName = TempTableName;
            bulkCopy.ColumnMappings.Add(nameof(Client.LastName), nameof(Client.LastName));
            bulkCopy.ColumnMappings.Add(nameof(Client.FirstName), nameof(Client.FirstName));
            await bulkCopy.WriteToServerAsync(table, ct);
        }
    }

    private async Task UpsertClientsAsync(SqlConnection connection, CancellationToken ct)
    {
        // Only insert names not already present, so re-running the import doesn't duplicate rows.
        const string sql = $"""
            INSERT INTO {TableName} (FirstName, LastName)
            SELECT s.FirstName, s.LastName
            FROM {TempTableName} s
            WHERE NOT EXISTS (
                SELECT 1 FROM {TableName} t
                WHERE t.FirstName = s.FirstName AND t.LastName = s.LastName
            );
        """;
        
        await using SqlCommand insertCmd = new(sql, connection);
        await insertCmd.ExecuteNonQueryAsync(ct);
    }
}
