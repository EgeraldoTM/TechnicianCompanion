using System.Text;
using DataMigration.Application.Interfaces;
using DataMigration.Application.Models;

namespace DataMigration.Application;

public sealed class CsvImportReportWriter : IImportReportWriter
{
    private static readonly string[] Headers =
        ["Row Index", "Successful", "Errors", "Technician", "Client", "Total", "Information"];

    public async Task InitializeAsync(string outputPath, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);

        string? directory = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

        await using FileStream stream = new(outputPath, FileMode.Create, FileAccess.Write, FileShare.None);
        await using StreamWriter writer = new(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
        await writer.WriteLineAsync(string.Join(',', Headers.Select(Escape)));
    }

    public async Task AppendAsync(IEnumerable<ImportResult> results, string outputPath, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(results);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);

        await using FileStream stream = new(outputPath, FileMode.Append, FileAccess.Write, FileShare.None);
        await using StreamWriter writer = new(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

        foreach (ImportResult result in results.OrderBy(result => result.RowIndex))
        {
            ct.ThrowIfCancellationRequested();
            string[] columns =
            [
                result.RowIndex.ToString(System.Globalization.CultureInfo.InvariantCulture),
                result.Successful ? "true" : "false", string.Join('\n', result.Errors.Select(error => error.Message)),
                result.Technician, result.Client, result.Total, result.Information
            ];
            await writer.WriteLineAsync(string.Join(',', columns.Select(Escape)));
        }
    }

    private static string Escape(string? value) =>
        $"\"{(value ?? string.Empty).Replace("\"", "\"\"")}\"";
}
