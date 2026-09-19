using System.Text.RegularExpressions;

namespace DataMigration.Matching;

public static partial class DateExtractor
{
    public static DateOnly? ExtractDate(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return null;

        var match = LittleEndianDateRegex().Match(input);
        return TryParse(match, out DateOnly date) ? date : null;
    }

    /// <summary>Removes the first valid date for parsing purposes only.</summary>
    public static string RemoveFirstDate(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return string.Empty;

        var match = LittleEndianDateRegex().Match(input);
        return TryParse(match, out _) ? input.Remove(match.Index, match.Length) : input;
    }

    private static bool TryParse(Match match, out DateOnly date)
    {
        date = default;
        if (!match.Success) return false;

        return int.TryParse(match.Groups[1].Value, out int day)
               && int.TryParse(match.Groups[3].Value, out int month)
               && int.TryParse(match.Groups[4].Value, out int year)
               && DateOnly.TryParseExact(
                   $"{day:D2}/{month:D2}/{year:D4}",
                   "dd/MM/yyyy",
                   System.Globalization.CultureInfo.InvariantCulture,
                   System.Globalization.DateTimeStyles.None,
                   out date);
    }

    [GeneratedRegex(@"\b(\d{1,2})([/.-])(\d{1,2})\2(\d{4})\b")]
    private static partial Regex LittleEndianDateRegex();
}
