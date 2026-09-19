using System.Globalization;
using System.Text;

namespace DataMigration.Core.Extensions;

public static class NameNormalizationExtensions
{
    public static string NormalizeName(this string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;

        string decomposed = value.Normalize(NormalizationForm.FormD);
        var result = new StringBuilder(decomposed.Length);
        bool previousWasSpace = true;

        foreach (char character in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark)
                continue;

            if (char.IsLetterOrDigit(character))
            {
                result.Append(char.ToLowerInvariant(character));
                previousWasSpace = false;
            }
            else if (!previousWasSpace)
            {
                result.Append(' ');
                previousWasSpace = true;
            }
        }

        return result.ToString().Trim();
    }
}
