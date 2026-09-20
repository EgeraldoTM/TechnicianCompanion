using DataMigration.Application.Interfaces.Repositories;
using DataMigration.Application.Models;
using Microsoft.Data.SqlClient;

namespace DataMigration.Database.Repositories;

public sealed class ImportRunRepository(string connectionString) : IImportRunRepository
{
    private readonly string _connectionString = connectionString;

    public async Task<ImportRun> GetOrCreateAsync(string sourceFileHash, string sourceFileName, CancellationToken ct)
    {
        await using SqlConnection connection = new(_connectionString);
        await connection.OpenAsync(ct);
        await using SqlTransaction transaction = (SqlTransaction)await connection.BeginTransactionAsync(ct);
        await using SqlCommand command = new("SELECT Id FROM ImportRuns WITH (UPDLOCK, HOLDLOCK) WHERE SourceFileHash = @hash;", connection, transaction);
        command.Parameters.AddWithValue("@hash", sourceFileHash);
        object? existing = await command.ExecuteScalarAsync(ct);
        Guid id;
        if (existing is Guid existingId) id = existingId;
        else
        {
            id = Guid.NewGuid();
            await using SqlCommand insert = new("INSERT INTO ImportRuns (Id, SourceFileHash, SourceFileName, Status) VALUES (@id, @hash, @name, 'InProgress');", connection, transaction);
            insert.Parameters.AddWithValue("@id", id);
            insert.Parameters.AddWithValue("@hash", sourceFileHash);
            insert.Parameters.AddWithValue("@name", sourceFileName);
            await insert.ExecuteNonQueryAsync(ct);
        }
        await transaction.CommitAsync(ct);
        return new ImportRun(id, sourceFileHash);
    }

    public async Task CompleteAsync(Guid importRunId, bool completedWithErrors, CancellationToken ct)
    {
        await using SqlConnection connection = new(_connectionString);
        await connection.OpenAsync(ct);
        await using SqlCommand command = new("UPDATE ImportRuns SET Status = @status, CompletedAtUtc = SYSUTCDATETIME() WHERE Id = @id;", connection);
        command.Parameters.AddWithValue("@id", importRunId);
        command.Parameters.AddWithValue("@status", completedWithErrors ? "CompletedWithErrors" : "Completed");
        await command.ExecuteNonQueryAsync(ct);
    }
}
