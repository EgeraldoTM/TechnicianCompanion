using System.Text.RegularExpressions;

namespace DataMigration.Core.Extensions;

public static partial class StringExtensions
{
    /// <summary>
    /// Splits "FirstName LastName" on the first space. Everything after the first
    /// space is treated as the last name, so multi-word last names stay together.
    /// </summary>
    public static (string FirstName, string LastName) SplitFullName(this string fullName)
    {
        string normalized = fullName.NormalizeWhitespace();
        int firstSpace = normalized.IndexOf(' ');

        if (firstSpace < 0) return (normalized, string.Empty);
        
        string firstName = normalized[..firstSpace];
        string lastName = normalized[(firstSpace + 1)..].Trim();
        
        return (firstName, lastName);
    }
    
    public static string NormalizeWhitespace(this string input)
    {
        if (string.IsNullOrEmpty(input)) return input;
        return WhitespaceRegex().Replace(input, " ").Trim();
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();
}
