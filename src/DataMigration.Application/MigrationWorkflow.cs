using DataMigration.Application.Exceptions;
using DataMigration.Application.Interfaces;
using DataMigration.Application.Interfaces.Repositories;
using DataMigration.Application.Models;
using DataMigration.Application.Models.Matching;
using DataMigration.Core.Extensions;
using DataMigration.Core.Models;

namespace DataMigration.Application;

public sealed class MigrationWorkflow(
    IExcelReader excelReader,
    IClientRepository clientRepository,
    ITechnicianRepository technicianRepository,
    IWorkOrderRepository workOrderRepository,
    IClientMatcher clientMatcher,
    INotesParser notesParser,
    IImportReportWriter reportWriter,
    int workOrderBatchSize = 10_000)
{
    private readonly IExcelReader _excelReader = excelReader;
    private readonly IClientRepository _clientRepository = clientRepository;
    private readonly ITechnicianRepository _technicianRepository = technicianRepository;
    private readonly IWorkOrderRepository _workOrderRepository = workOrderRepository;
    private readonly IClientMatcher _clientMatcher = clientMatcher;
    private readonly INotesParser _notesParser = notesParser;
    private readonly IImportReportWriter _reportWriter = reportWriter;
    private readonly int _workOrderBatchSize = workOrderBatchSize > 0
        ? workOrderBatchSize
        : throw new ArgumentOutOfRangeException(nameof(workOrderBatchSize), "Batch size must be greater than zero.");

    public async Task<MigrationSummary> RunAsync(
        MigrationRequest request,
        IProgress<MigrationProgress>? progress,
        CancellationToken ct)
    {
        ValidateExcelFilePath(request.ClientsFilePath, "clients");
        ValidateExcelFilePath(request.WorkOrdersFilePath, "work-orders");

        var insertedClients = await MigrateClientsAsync(request.ClientsFilePath, ct);
        var insertedTechnicians = await MigrateTechniciansAsync(request.WorkOrdersFilePath, ct);

        _clientMatcher.BuildIndex(insertedClients);
        var techniciansByName = insertedTechnicians
            .GroupBy(technician => technician.FullName.NormalizeName(), StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);

        string reportPath = Path.Combine(
            Path.GetDirectoryName(Path.GetFullPath(request.WorkOrdersFilePath))!,
            "work-order-import-report.csv");
        await _reportWriter.InitializeAsync(reportPath, ct);

        List<WorkOrder> validWorkOrders = [];
        List<ImportResult> results = [];
        int processed = 0;
        int succeeded = 0;
        int failed = 0;

        await foreach (WorkOrderExcelRow row in _excelReader.ReadWorkOrdersAsync(request.WorkOrdersFilePath, ct))
        {
            ct.ThrowIfCancellationRequested();
            var workOrderResult = ConstructWorkOrder(row, techniciansByName);
            results.Add(workOrderResult.Result);
            if (workOrderResult.WorkOrder is not null)
                validWorkOrders.Add(workOrderResult.WorkOrder);

            processed++;
            progress?.Report(new MigrationProgress(processed, 0));

            if (results.Count == _workOrderBatchSize)
            {
                MigrationBatchSummary batchSummary = await PersistBatchAsync(validWorkOrders, results, reportPath, ct);
                succeeded += batchSummary.Succeeded;
                failed += batchSummary.Failed;
            }
        }

        if (results.Count > 0)
        {
            MigrationBatchSummary batchSummary = await PersistBatchAsync(validWorkOrders, results, reportPath, ct);
            succeeded += batchSummary.Succeeded;
            failed += batchSummary.Failed;
        }

        return new MigrationSummary(succeeded, failed, reportPath);
    }

    private async Task<IReadOnlyList<Client>> MigrateClientsAsync(string path, CancellationToken ct)
    {
        HashSet<string> seenClients = new(StringComparer.OrdinalIgnoreCase);
        List<Client> clients = [];
        await foreach (string fullName in _excelReader.ReadClientsAsync(path, ct))
        {
            ct.ThrowIfCancellationRequested();
            if (!seenClients.Add(fullName)) continue;
            (string firstName, string lastName) = fullName.SplitFullName();
            clients.Add(new Client(firstName, lastName));
        }

        return await _clientRepository.UpsertClientsAsync(clients, ct);
    }

    private async Task<IReadOnlyList<Technician>> MigrateTechniciansAsync(string path, CancellationToken ct)
    {
        HashSet<string> seenTechnicians = new(StringComparer.OrdinalIgnoreCase);
        List<Technician> technicians = [];
        await foreach (string fullName in _excelReader.ReadTechniciansAsync(path, ct))
        {
            ct.ThrowIfCancellationRequested();
            if (!seenTechnicians.Add(fullName)) continue;
            (string firstName, string lastName) = fullName.SplitFullName();
            technicians.Add(new Technician(firstName, lastName));
        }

        return await _technicianRepository.UpsertTechniciansAsync(technicians, ct);
    }

    private WorkOrderResult ConstructWorkOrder(WorkOrderExcelRow row, Dictionary<string, Technician> techniciansByName)
    {
        ParsedNotes parsedNotes = _notesParser.Parse(row.Notes);
        List<ImportError> errors = [];

        Technician? technician = null;
        string technicianKey = row.Technician.NormalizeName();
        if (technicianKey.Length == 0 || !techniciansByName.TryGetValue(technicianKey, out technician))
            errors.Add(ImportErrors.TechnicianMissingOrUnmatched);

        Client? client = null;
        string clientForReport = parsedNotes.ClientName ?? string.Empty;
        if (string.IsNullOrWhiteSpace(parsedNotes.ClientName))
        {
            MatchResult<Client> match = _clientMatcher.Match(row.Notes);
            if (match.Outcome != MatchOutcome.Confident || match.Matched is null)
                errors.Add(ImportErrors.ClientMissing);
            else
            {
                client = match.Matched;
                clientForReport = client.FullName;
            }
        }
        else
        {
            MatchResult<Client> match = _clientMatcher.Match(parsedNotes.ClientName);
            bool matchedRawNotes = false;
            if (match.Outcome != MatchOutcome.Confident || match.Matched is null)
            {
                match = _clientMatcher.Match(row.Notes);
                matchedRawNotes = true;
            }

            if (match.Outcome != MatchOutcome.Confident || match.Matched is null)
                errors.Add(ImportErrors.ClientUnmatched);
            else
            {
                client = match.Matched;
                clientForReport = matchedRawNotes ? client.FullName : parsedNotes.ClientName;
            }
        }

        if (!parsedNotes.Date.HasValue)
            errors.Add(ImportErrors.DateMissingOrInvalid);

        if (!decimal.TryParse(row.Total, out decimal total))
            errors.Add(ImportErrors.TotalMissingOrInvalid);

        ImportResult result = new(
            row.RowNumber,
            errors.Count == 0,
            errors,
            row.Technician,
            clientForReport,
            row.Total,
            parsedNotes.Information);

        if (errors.Count > 0) return new WorkOrderResult(null, result);

        WorkOrder workOrder = new()
        {
            TechnicianId = technician!.Id,
            ClientId = client!.Id,
            Date = parsedNotes.Date!.Value,
            Total = total,
            Information = parsedNotes.Information
        };
        return new WorkOrderResult(workOrder, result);
    }

    private sealed record WorkOrderResult(WorkOrder? WorkOrder, ImportResult Result);
    private sealed record MigrationBatchSummary(int Succeeded, int Failed);

    private async Task<MigrationBatchSummary> PersistBatchAsync(
        List<WorkOrder> validWorkOrders,
        List<ImportResult> results,
        string reportPath,
        CancellationToken ct)
    {
        await _workOrderRepository.BulkInsertAsync(validWorkOrders, ct);
        await _reportWriter.AppendAsync(results, reportPath, ct);

        MigrationBatchSummary summary = new(validWorkOrders.Count, results.Count(result => !result.Successful));
        validWorkOrders.Clear();
        results.Clear();
        return summary;
    }

    private static void ValidateExcelFilePath(string filePath, string fileDescription)
    {
        if (!string.Equals(Path.GetExtension(filePath), ".xlsx", StringComparison.OrdinalIgnoreCase))
        {
            throw new ImportFileValidationException(
                $"The {fileDescription} file must have a .xlsx extension: '{filePath}'.");
        }
    }
}
