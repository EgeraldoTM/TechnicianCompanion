using DataMigration.Core.Models;

namespace DataMigration.Application.Models;

public sealed record WorkOrderImportItem(WorkOrder? WorkOrder, ImportResult Result);
