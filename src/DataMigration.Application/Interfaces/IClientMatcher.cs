using DataMigration.Application.Models.Matching;
using DataMigration.Core.Models;

namespace DataMigration.Application.Interfaces;

public interface IClientMatcher
{
    void BuildIndex(IEnumerable<Client> clients);
    MatchResult<Client> Match(string extractedName);
}
