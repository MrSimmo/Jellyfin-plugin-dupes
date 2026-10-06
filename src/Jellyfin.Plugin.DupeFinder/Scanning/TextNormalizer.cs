using System;
using System.Text;

namespace Jellyfin.Plugin.DupeFinder.Scanning;

/// <summary>
/// Spec §4 "normalised text": NFKD, marks removed, lower case, <c>&amp;</c> → "and", letters and digits only.
/// </summary>
internal static class TextNormalizer
{
    public static string Normalize(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        var decomposed = text.Replace("&", " and ", StringComparison.Ordinal).Normalize(NormalizationForm.FormKD);
        var builder = new StringBuilder(decomposed.Length);
        foreach (var c in decomposed)
        {
            // Combining marks left by NFKD are not letters or digits, so accents drop out here.
            if (char.IsLetterOrDigit(c))
            {
                builder.Append(char.ToLowerInvariant(c));
            }
        }

        return builder.ToString();
    }
}
