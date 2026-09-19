using DataMigration.Application;
using DataMigration.Application.Exceptions;
using DataMigration.Application.Interfaces;
using DataMigration.Application.Interfaces.Repositories;
using DataMigration.Application.Models;
using DataMigration.Core.Models;
using DataMigration.Matching;
using Xunit;

namespace DataMigration.Tests;

public sealed class MigrationWorkflowTests
{
    [Fact]
    public async Task RunAsync_PersistsWorkOrdersAndReportRowsInConfiguredBatches()
    {
        var client = new Client("Andi", "Muçobegaj") { Id = 10 };
        var technician = new Technician("Arben", "Hoxha") { Id = 20 };
        var workOrderRepository = new RecordingWorkOrderRepository();
        var reportWriter = new RecordingReportWriter();
        var workflow = new MigrationWorkflow(
            new FakeExcelReader(
                ["Andi Muçobegaj"],
                ["Arben Hoxha"],
                [
                    new WorkOrderExcelRow(2, "Arben Hoxha", "Client: Andi Mucobega 12/03/2024", "100"),
                    new WorkOrderExcelRow(3, "Arben Hoxha", "Client: Andi Mucobega 13/03/2024", "110"),
                    new WorkOrderExcelRow(4, "Arben Hoxha", "Client: Andi Mucobega 14/03/2024", "120")
                ]),
            new FakeClientRepository(client),
            new FakeTechnicianRepository(technician),
            workOrderRepository,
            new ClientMatcher(),
            new NotesParser(),
            reportWriter,
            workOrderBatchSize: 2);

        await workflow.RunAsync(new MigrationRequest("clients.xlsx", "work-orders.xlsx"), null, CancellationToken.None);

        Assert.Equal([2, 1], workOrderRepository.BatchSizes);
        Assert.Equal([2, 1], reportWriter.BatchSizes);
    }

    [Fact]
    public async Task RunAsync_WhenClientsPathIsNotAnExcelFile_ThrowsAValidationErrorBeforeReadingFiles()
    {
        var workflow = new MigrationWorkflow(
            new FakeExcelReader([], [], []),
            new FakeClientRepository(new Client("Andi", "Muçobegaj")),
            new FakeTechnicianRepository(new Technician("Arben", "Hoxha")),
            new RecordingWorkOrderRepository(),
            new ClientMatcher(),
            new NotesParser(),
            new RecordingReportWriter());

        ImportFileValidationException exception = await Assert.ThrowsAsync<ImportFileValidationException>(
            () => workflow.RunAsync(new MigrationRequest("clients.csv", "work-orders.xlsx"), null, CancellationToken.None));

        Assert.Equal("The clients file must have a .xlsx extension: 'clients.csv'.", exception.Message);
    }

    [Fact]
    public async Task RunAsync_BulkInsertsOnlyValidWorkOrders_AndReportsAllValidationErrors()
    {
        var client = new Client("Andi", "Muçobegaj") { Id = 10 };
        var technician = new Technician("Arben", "Hoxha") { Id = 20 };
        var excelReader = new FakeExcelReader(
            ["Andi Muçobegaj"],
            ["Arben Hoxha"],
            [
                new WorkOrderExcelRow(3, "Arben Hoxha", "Client: Andi Mucobega\n12/03/2024", "125"),
                new WorkOrderExcelRow(2, "", "Kontroll teknik", "invalid")
            ]);
        var workOrderRepository = new RecordingWorkOrderRepository();
        var reportWriter = new RecordingReportWriter();
        var workflow = new MigrationWorkflow(
            excelReader,
            new FakeClientRepository(client),
            new FakeTechnicianRepository(technician),
            workOrderRepository,
            new ClientMatcher(),
            new NotesParser(),
            reportWriter);

        MigrationSummary summary = await workflow.RunAsync(
            new MigrationRequest("clients.xlsx", "work-orders.xlsx"), null, CancellationToken.None);

        Assert.Equal(1, summary.Succeeded);
        Assert.Equal(1, summary.Failed);
        WorkOrder workOrder = Assert.Single(workOrderRepository.WorkOrders);
        Assert.Equal(20, workOrder.TechnicianId);
        Assert.Equal(10, workOrder.ClientId);
        Assert.Equal(new DateOnly(2024, 3, 12), workOrder.Date);
        Assert.Equal("Client: Andi Mucobega\n12/03/2024", workOrder.Information);

        ImportResult failedResult = Assert.Single(reportWriter.Results, result => !result.Successful);
        Assert.Equal(2, failedResult.RowIndex);
        Assert.Equal(
            [
                ImportErrors.TechnicianMissingOrUnmatched,
                ImportErrors.ClientMissing,
                ImportErrors.DateMissingOrInvalid,
                ImportErrors.TotalMissingOrInvalid
            ],
            failedResult.Errors);
    }

    private sealed class FakeExcelReader(
        IEnumerable<string> clients,
        IEnumerable<string> technicians,
        IEnumerable<WorkOrderExcelRow> workOrders) : IExcelReader
    {
        public async IAsyncEnumerable<string> ReadClientsAsync(string filePath, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
        {
            foreach (string client in clients) { ct.ThrowIfCancellationRequested(); yield return client; }
            await Task.CompletedTask;
        }

        public async IAsyncEnumerable<string> ReadTechniciansAsync(string filePath, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
        {
            foreach (string technician in technicians) { ct.ThrowIfCancellationRequested(); yield return technician; }
            await Task.CompletedTask;
        }

        public async IAsyncEnumerable<WorkOrderExcelRow> ReadWorkOrdersAsync(string filePath, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
        {
            foreach (WorkOrderExcelRow workOrder in workOrders) { ct.ThrowIfCancellationRequested(); yield return workOrder; }
            await Task.CompletedTask;
        }
    }

    private sealed class FakeClientRepository(Client client) : IClientRepository
    {
        public Task<IReadOnlyList<Client>> UpsertClientsAsync(IEnumerable<Client> clients, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<Client>>([client]);
    }

    private sealed class FakeTechnicianRepository(Technician technician) : ITechnicianRepository
    {
        public Task<IReadOnlyList<Technician>> UpsertTechniciansAsync(IEnumerable<Technician> technicians, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<Technician>>([technician]);
    }

    private sealed class RecordingWorkOrderRepository : IWorkOrderRepository
    {
        public List<WorkOrder> WorkOrders { get; } = [];
        public List<int> BatchSizes { get; } = [];

        public Task BulkInsertAsync(IEnumerable<WorkOrder> workOrders, CancellationToken ct)
        {
            List<WorkOrder> batch = [.. workOrders];
            BatchSizes.Add(batch.Count);
            WorkOrders.AddRange(batch);
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingReportWriter : IImportReportWriter
    {
        public List<ImportResult> Results { get; } = [];
        public List<int> BatchSizes { get; } = [];

        public Task InitializeAsync(string outputPath, CancellationToken ct) => Task.CompletedTask;

        public Task AppendAsync(IEnumerable<ImportResult> results, string outputPath, CancellationToken ct)
        {
            List<ImportResult> batch = [.. results];
            BatchSizes.Add(batch.Count);
            Results.AddRange(batch);
            return Task.CompletedTask;
        }
    }
}
