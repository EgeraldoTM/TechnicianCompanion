namespace DataMigration.Application.Models;

public sealed record ImportRun(Guid Id, string SourceFileHash);
