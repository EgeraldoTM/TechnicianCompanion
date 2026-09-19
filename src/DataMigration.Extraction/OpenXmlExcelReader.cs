using System.Runtime.CompilerServices;
using DataMigration.Application.Interfaces;
using DataMigration.Application.Models;
using DataMigration.Application.Exceptions;
using DataMigration.Extraction.Extensions;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;

namespace DataMigration.Extraction;

public class OpenXmlExcelReader : IExcelReader
{
    private const string ClientColumnName = "Client";
    private const string TechnicianColumnName = "Technician";
    private const string NotesColumnName = "Notes";
    private const string TotalColumnName = "Total";
    private static readonly string[] ClientHeaders = [ClientColumnName];
    private static readonly string[] WorkOrderHeaders = [TechnicianColumnName, NotesColumnName, TotalColumnName];
    
    // TODO: technicians and client with empty first + last names are skipped, those with empty last names are inserted
    // TODO: Lastly, you can use TPL to do the job in parallel (reading + bulk inserting)
    public async IAsyncEnumerable<WorkOrderExcelRow> ReadWorkOrdersAsync(string filePath, [EnumeratorCancellation] CancellationToken ct)
    {
        await foreach ((string[] values, string[] headers, int rowIndex) in ReadRawRowsAsync(
                           filePath, "work-orders", WorkOrderHeaders, requireExactHeaders: false, ct))
        {
            yield return new WorkOrderExcelRow(
                RowNumber: rowIndex,
                Technician: GetValue(values, headers, TechnicianColumnName),
                Notes: GetValue(values, headers, NotesColumnName),
                Total: GetValue(values, headers, TotalColumnName));
        }
    }

    public async IAsyncEnumerable<string> ReadClientsAsync(string filePath, [EnumeratorCancellation] CancellationToken ct)
    {
        await foreach ((string[] values, string[] headers, _) in ReadRawRowsAsync(
                           filePath, "clients", ClientHeaders, requireExactHeaders: true, ct))
        {
            string clientFullName = GetValue(values, headers, ClientColumnName);
            if (string.IsNullOrWhiteSpace(clientFullName)) continue;
            yield return clientFullName;
        }
    }

    public async IAsyncEnumerable<string> ReadTechniciansAsync(string filePath, [EnumeratorCancellation] CancellationToken ct)
    {
        await foreach ((string[] values, string[] headers, _) in ReadRawRowsAsync(
                           filePath, "work-orders", WorkOrderHeaders, requireExactHeaders: false, ct))
        {
            string technicianFullName = GetValue(values, headers, TechnicianColumnName);
            if (string.IsNullOrWhiteSpace(technicianFullName)) continue;
            yield return technicianFullName;
        }
    }

    private static async IAsyncEnumerable<(string[] Values, string[] Headers, int RowIndex)> ReadRawRowsAsync(
        string filePath,
        string fileDescription,
        IReadOnlyCollection<string> requiredHeaders,
        bool requireExactHeaders,
        [EnumeratorCancellation] CancellationToken ct)
    {
        using var doc = SpreadsheetDocument.Open(filePath, isEditable: false);
        var workbookPart = doc.WorkbookPart ?? throw new InvalidOperationException("Workbook part missing.");
        string[] sharedStrings = workbookPart.LoadSharedStrings();
        var sheetPart = GetFirstWorksheetPart(workbookPart);

        using var reader = OpenXmlReader.Create(sheetPart);

        string[]? headers = null;
        int rowIndex = 0;

        while (reader.Read())
        {
            ct.ThrowIfCancellationRequested();
            if (reader.ElementType != typeof(Row) || !reader.IsStartElement) continue;

            var row = (Row)reader.LoadCurrentElement()!;
            rowIndex++;
            string[] values = row.ReadRowValues(sharedStrings);

            if (rowIndex == 1)
            {
                headers = values.Select(header => header.Trim()).ToArray();
                ValidateHeaders(headers, fileDescription, requiredHeaders, requireExactHeaders);
                continue;
            }
            
            if (values.All(string.IsNullOrWhiteSpace)) continue;

            yield return (values, headers!, rowIndex);
        }

        if (headers is null)
            ValidateHeaders([], fileDescription, requiredHeaders, requireExactHeaders);

        await Task.CompletedTask;
    }

    private static WorksheetPart GetFirstWorksheetPart(WorkbookPart workbookPart)
    {
        var sheetId = workbookPart.Workbook!.Sheets!.Elements<Sheet>().First().Id!.Value!;
        return (WorksheetPart)workbookPart.GetPartById(sheetId);
    }

    private static string GetValue(string[] values, string[] headers, string headerName)
    {
        int col = Array.IndexOf(headers, headerName);
        return col >= 0 && col < values.Length ? values[col].Trim() : string.Empty;
    }

    private static void ValidateHeaders(
        IReadOnlyCollection<string> headers,
        string fileDescription,
        IReadOnlyCollection<string> requiredHeaders,
        bool requireExactHeaders)
    {
        List<string> missingHeaders = requiredHeaders
            .Where(header => !headers.Contains(header, StringComparer.Ordinal))
            .ToList();
        bool hasUnexpectedHeaders = requireExactHeaders
            && (headers.Count != requiredHeaders.Count
                || headers.Any(header => !requiredHeaders.Contains(header, StringComparer.Ordinal)));

        if (missingHeaders.Count == 0 && !hasUnexpectedHeaders) return;

        string expected = string.Join(", ", requiredHeaders.Select(header => $"'{header}'"));
        string found = headers.Count == 0 ? "no headers" : string.Join(", ", headers.Select(header => $"'{header}'"));
        string requirement = requireExactHeaders
            ? $"contain exactly one header: {expected}"
            : $"contain the headers: {expected}";

        throw new ImportFileValidationException(
            $"The {fileDescription} file must {requirement}. Found {found}.");
    }
}
