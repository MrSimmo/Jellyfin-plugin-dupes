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

        // NFKD first, so compatibility forms such as fullwidth "＆" become "&" before the replacement.
        var decomposed = text.Normalize(NormalizationForm.FormKD).Replace("&", " and ", StringComparison.Ordinal);
        var builder = new StringBuilder(decomposed.Length);

        // One rune is at most two UTF-16 code units.
        Span<char> buffer = stackalloc char[2];

        // Runes, not chars: a lone surrogate half is not a letter, so a char loop would drop
        // supplementary-plane letters such as "𠮷".
        foreach (var rune in decomposed.EnumerateRunes())
        {
            // Combining marks left by NFKD are not letters or digits, so accents drop out here.
            if (Rune.IsLetterOrDigit(rune))
            {
                var written = Rune.ToLowerInvariant(rune).EncodeToUtf16(buffer);
                builder.Append(buffer[..written]);
            }
        }

        return builder.ToString();
    }
}
