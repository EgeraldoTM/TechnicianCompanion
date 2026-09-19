using DataMigration.Application.Interfaces;
using DataMigration.Application.Models.Matching;
using DataMigration.Core.Extensions;
using DataMigration.Core.Models;

namespace DataMigration.Matching;

/// <summary>
/// Matches a client name extracted from Notes against the client records loaded from the database.
/// The matcher deliberately reports close competing results as ambiguous instead of selecting one.
/// </summary>
public sealed class ClientMatcher : IClientMatcher
{
    private const int ConfidentScore = 80;
    private const int AmbiguousScore = 75;
    private const int RequiredLeadOverSecondPlace = 8;

    private Client[] _clients = [];
    private Dictionary<string, List<Client>> _exactNames = new(StringComparer.Ordinal);

    public void BuildIndex(IEnumerable<Client> clients)
    {
        ArgumentNullException.ThrowIfNull(clients);

        _clients = [.. clients];
        _exactNames = _clients
            .GroupBy(client => client.FullName.NormalizeName(), StringComparer.Ordinal)
            .Where(group => group.Key.Length > 0)
            .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.Ordinal);
    }

    public MatchResult<Client> Match(string extractedName)
    {
        string normalizedName = extractedName.NormalizeName();
        if (normalizedName.Length == 0 || _clients.Length == 0)
            return new MatchResult<Client>(null, 0, MatchOutcome.NoMatch);

        if (_exactNames.TryGetValue(normalizedName, out List<Client>? exactMatches))
        {
            return exactMatches.Count == 1
                ? new MatchResult<Client>(exactMatches[0], 1, MatchOutcome.Confident)
                : new MatchResult<Client>(null, 1, MatchOutcome.Ambiguous);
        }

        var ranked = _clients
            .Select(client => new Candidate(client, Score(client, extractedName)))
            .OrderByDescending(candidate => candidate.Score)
            .ToArray();

        Candidate best = ranked[0];
        int runnerUpScore = ranked.Length > 1 ? ranked[1].Score : 0;
        double confidence = best.Score / 100d;

        if (best.Score >= ConfidentScore && best.Score - runnerUpScore >= RequiredLeadOverSecondPlace)
            return new MatchResult<Client>(best.Client, confidence, MatchOutcome.Confident);

        if (best.Score >= AmbiguousScore)
            return new MatchResult<Client>(null, confidence, MatchOutcome.Ambiguous);

        return new MatchResult<Client>(null, confidence, MatchOutcome.NoMatch);
    }

    private sealed record Candidate(Client Client, int Score);

    private static int Score(Client client, string extractedName)
    {
        string normalizedExtractedName = extractedName.NormalizeName();
        string normalizedClientName = client.FullName.NormalizeName();
        int score = normalizedExtractedName.SimilarityTo(normalizedClientName);

        string[] extractedWords = normalizedExtractedName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        int clientWordCount = normalizedClientName.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
        if (extractedWords.Length <= clientWordCount) return score;

        for (int index = 0; index <= extractedWords.Length - clientWordCount; index++)
        {
            string candidate = string.Join(' ', extractedWords.Skip(index).Take(clientWordCount));
            score = Math.Max(score, candidate.SimilarityTo(normalizedClientName));
        }

        return score;
    }
}
