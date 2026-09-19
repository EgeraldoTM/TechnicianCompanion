using System.Text.RegularExpressions;
using DataMigration.Application.Interfaces;
using DataMigration.Application.Models;

namespace DataMigration.Matching;

/// <summary>
/// Extracts the date and a client-name candidate from common spreadsheet Notes formats.
/// A database-backed <see cref="ClientMatcher"/> should be used to validate the candidate.
/// </summary>
public sealed partial class NotesParser : INotesParser
{
    public ParsedNotes Parse(string rawNotes)
    {
        string information = rawNotes ?? string.Empty;
        string notes = information.Trim();
        if (notes.Length == 0) return new ParsedNotes(null, null, string.Empty);

        DateOnly? date = DateExtractor.ExtractDate(notes);
        string withoutDate = DateExtractor.RemoveFirstDate(notes);
        string? clientName = ExtractLabelledClient(withoutDate)
            ?? ExtractClientMention(withoutDate)
            ?? ExtractCapitalizedName(withoutDate)
            ?? ExtractNameOnlySegment(withoutDate);

        return new ParsedNotes(date, clientName, information);
    }

    private static string? ExtractLabelledClient(string notes)
    {
        Match match = ClientLabelRegex().Match(notes);
        return match.Success ? CleanCandidate(match.Groups[1].Value) : null;
    }

    private static string? ExtractClientMention(string notes)
    {
        Match match = ClientMentionRegex().Match(notes);
        return match.Success ? CleanCandidate(match.Groups["name"].Value) : null;
    }

    private static string? ExtractCapitalizedName(string notes)
    {
        MatchCollection matches = CapitalizedNameRegex().Matches(notes);
        if (matches.Count == 0) return null;

        // The client normally appears after the work description, so prefer the final name-like phrase.
        return CleanCandidate(matches[^1].Groups["name"].Value);
    }

    // Used for Notes such as "Andi Mucobega - repaired boiler". We only infer a name when
    // the first segment has 2-4 words consisting solely of letters (including Albanian diacritics).
    private static string? ExtractNameOnlySegment(string notes)
    {
        string firstSegment = notes.Split(['-', '–', '—', ';', '|', ',', '\n', '\r'], 2)[0];
        return LooksLikeUnlabelledName(firstSegment) ? CleanCandidate(firstSegment) : null;
    }

    private static string? CleanCandidate(string value)
    {
        string candidate = NormalizeWhitespace(value.Trim(' ', '-', '–', '—', ';', ',', ':', '|'));
        return LooksLikeName(candidate) ? candidate : null;
    }

    private static bool LooksLikeName(string value)
    {
        string[] words = value.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return words.Length is >= 2 and <= 4 && words.All(word => word.All(char.IsLetter));
    }

    private static bool LooksLikeUnlabelledName(string value)
    {
        if (!LooksLikeName(value)) return false;

        return value.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .All(word => char.IsUpper(word[0]) || word.All(char.IsUpper));
    }

    private static string NormalizeWhitespace(string? value) =>
        WhitespaceRegex().Replace(value ?? string.Empty, " ").Trim();

    [GeneratedRegex(@"(?:^|[;|,\n\r]\s*)\s*(?:client|customer|klient(?:i)?|emri(?:\s+i\s+klientit)?)\s*[:=-]\s*([^;|,\n\r]+)", RegexOptions.IgnoreCase)]
    private static partial Regex ClientLabelRegex();

    [GeneratedRegex(@"\b(?:klient(?:i|it|in)?|perdorues(?:i|in)?|te|per|nga)\s+(?:klienti\s+)?(?<name>\p{Lu}[\p{L}'-]*(?:\s+\p{Lu}[\p{L}'-]*){1,2})")]
    private static partial Regex ClientMentionRegex();

    [GeneratedRegex(@"(?<name>\p{Lu}[\p{L}'-]*(?:\s+\p{Lu}[\p{L}'-]*){1,2})")]
    private static partial Regex CapitalizedNameRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();
}
