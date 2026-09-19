using DataMigration.Core.Models;

namespace DataMigration.Application.Interfaces.Repositories;

public interface IClientRepository
{
    Task<IReadOnlyList<Client>> UpsertClientsAsync(IEnumerable<Client> clients, CancellationToken ct);
}
