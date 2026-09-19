using DataMigration.Core.Extensions;
using FuzzySharp;

namespace DataMigration.Matching;

public static class NameMatchingExtensions
{
    public static int SimilarityTo(this string? source, string? target)
    {
        string normalizedSource = source.NormalizeName();
        string normalizedTarget = target.NormalizeName();

        if (normalizedSource.Length == 0 || normalizedTarget.Length == 0) return 0;
        if (normalizedSource == normalizedTarget) return 100;

        // Token sorting also handles notes where first and last names were entered in reverse order.
        return Math.Max(
            Fuzz.Ratio(normalizedSource, normalizedTarget),
            Fuzz.TokenSortRatio(normalizedSource, normalizedTarget));
    }
}
