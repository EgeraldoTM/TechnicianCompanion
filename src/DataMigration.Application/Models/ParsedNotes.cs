namespace DataMigration.Application.Models;

/// <summary>
/// The structured values found in a work-order Notes cell.
/// </summary>
public sealed record ParsedNotes(
    DateOnly? Date,
    string? ClientName,
    string Information);
