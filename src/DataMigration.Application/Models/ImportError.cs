namespace DataMigration.Application.Models;

/// <summary>A typed validation error that can be reported to a user or handled by code.</summary>
public sealed record ImportError(string Code, string Message);

public static class ImportErrors
{
    public static readonly ImportError TechnicianMissingOrUnmatched = new(
        "technician_missing_or_unmatched",
        "Technician is missing or could not be matched.");

    public static readonly ImportError ClientMissing = new(
        "client_missing",
        "Client is missing from the notes.");

    public static readonly ImportError ClientUnmatched = new(
        "client_unmatched",
        "Client could not be matched.");

    public static readonly ImportError DateMissingOrInvalid = new(
        "date_missing_or_invalid",
        "Date is missing or invalid.");

    public static readonly ImportError TotalMissingOrInvalid = new(
        "total_missing_or_invalid",
        "Total is missing or invalid.");

    public static readonly ImportError BatchPersistenceFailed = new(
        "batch_persistence_failed",
        "The work-order batch could not be saved and can be retried.");
}
