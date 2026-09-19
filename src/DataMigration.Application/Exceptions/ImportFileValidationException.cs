namespace DataMigration.Application.Exceptions;

/// <summary>Thrown when an import file cannot be processed safely.</summary>
public sealed class ImportFileValidationException(string message) : Exception(message);
