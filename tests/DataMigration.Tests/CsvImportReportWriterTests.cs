using DataMigration.Application;
using DataMigration.Application.Models;
using Xunit;

namespace DataMigration.Tests;

public sealed class CsvImportReportWriterTests
{
    [Fact]
    public async Task AppendAsync_OrdersRowsAndKeepsMultipleErrorsInOneCsvCell()
    {
        string path = Path.Combine(Path.GetTempPath(), $"migration-report-{Guid.NewGuid():N}.csv");
        try
        {
            var writer = new CsvImportReportWriter();
            await writer.InitializeAsync(path, CancellationToken.None);
            await writer.AppendAsync(
            [
                new ImportResult(5, true, [], "Arben Hoxha", "Andi Mucobega", "100", "Finished"),
                new ImportResult(2, false, [ImportErrors.DateMissingOrInvalid, ImportErrors.TotalMissingOrInvalid],
                    "Arben Hoxha", "", "", "Raw note")
            ], path, CancellationToken.None);

            string csv = await File.ReadAllTextAsync(path);

            Assert.StartsWith("\"Row Index\",\"Successful\",\"Errors\",\"Technician\",\"Client\",\"Total\",\"Information\"", csv);
            Assert.True(csv.IndexOf("\"2\"", StringComparison.Ordinal) < csv.IndexOf("\"5\"", StringComparison.Ordinal));
            Assert.Contains("Date is missing or invalid.\nTotal is missing or invalid.", csv);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }
}
