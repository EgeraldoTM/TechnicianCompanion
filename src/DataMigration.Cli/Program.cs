using DataMigration.Application;
using DataMigration.Application.Exceptions;
using DataMigration.Application.Models;
using DataMigration.Database.Repositories;
using DataMigration.Extraction;
using DataMigration.Matching;

var options = ParseOptions(args);
if (!TryGetRequiredOption(options, "clients-file", out string clientsFile)
    || !TryGetRequiredOption(options, "work-orders-file", out string workOrdersFile)
    || !TryGetRequiredOption(options, "connection-string", out string connectionString))
{
    Console.Error.WriteLine("Usage: DataMigration.Cli --clients-file <path> --work-orders-file <path> --connection-string <connection string>");
    return 1;
}

var workflow = new MigrationWorkflow(
    new OpenXmlExcelReader(),
    new ClientRepository(connectionString),
    new TechnicianRepository(connectionString),
    new WorkOrderRepository(connectionString),
    new ClientMatcher(),
    new NotesParser(),
    new CsvImportReportWriter());

var progress = new Progress<MigrationProgress>(value => Console.Write($"\rProcessed {value.RowsProcessed} rows..."));

MigrationSummary summary;
try
{
    summary = await workflow.RunAsync(
        new MigrationRequest(clientsFile, workOrdersFile), progress, CancellationToken.None);
}
catch (ImportFileValidationException exception)
{
    Console.Error.WriteLine($"Import validation failed: {exception.Message}");
    return 1;
}

Console.WriteLine();
Console.WriteLine($"Done. Succeeded: {summary.Succeeded}, Failed: {summary.Failed}. Report: {summary.ReportFilePath}");
return 0;

static Dictionary<string, string> ParseOptions(string[] args)
{
    var options = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    for (int index = 0; index < args.Length; index++)
    {
        if (!args[index].StartsWith("--", StringComparison.Ordinal) || index + 1 >= args.Length) continue;
        options[args[index][2..]] = args[++index];
    }
    return options;
}

static bool TryGetRequiredOption(
    IReadOnlyDictionary<string, string> options,
    string name,
    out string value) =>
    options.TryGetValue(name, out value!) && !string.IsNullOrWhiteSpace(value);
