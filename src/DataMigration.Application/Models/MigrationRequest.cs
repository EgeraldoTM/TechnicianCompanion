namespace DataMigration.Application.Models;

public record MigrationRequest(string ClientsFilePath, string WorkOrdersFilePath);
