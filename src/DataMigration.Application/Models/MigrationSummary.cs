namespace DataMigration.Application.Models;

public record MigrationSummary(int Succeeded, int Failed, string ReportFilePath);
