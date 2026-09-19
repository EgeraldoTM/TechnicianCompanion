using DataMigration.Core.Models;

namespace DataMigration.Application.Interfaces.Repositories;

public interface ITechnicianRepository
{
    Task<IReadOnlyList<Technician>> UpsertTechniciansAsync(IEnumerable<Technician> technicians, CancellationToken ct);
}
