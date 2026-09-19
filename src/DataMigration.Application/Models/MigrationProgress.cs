namespace DataMigration.Application.Models;

public record MigrationProgress(int RowsProcessed, int TotalRows);
