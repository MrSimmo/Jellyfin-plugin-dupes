using System.Collections.Generic;
using System.Linq;
using Jellyfin.Plugin.DupeFinder.Model;

namespace Jellyfin.Plugin.DupeFinder.Scanning;

/// <summary>
/// Unmatched and incomplete-metadata rules (DF-R4). Callers choose which item kinds to pass.
/// </summary>
internal static class UnmatchedDetector
{
    public static IReadOnlyList<Finding> FindUnmatched(IReadOnlyList<ScannedItem> items, CheckId check)
        => items
            .Where(item => !item.HasAnyProviderId)
            .Select(item => Finding.From(item, check, "No provider ids"))
            .ToList();

    public static IReadOnlyList<Finding> FindIncomplete(IReadOnlyList<ScannedItem> items)
    {
        var findings = new List<Finding>();
        foreach (var item in items)
        {
            // Unmatched items belong to the unmatched checks, not here (AC-6).
            if (!item.HasAnyProviderId)
            {
                continue;
            }

            var missing = new List<string>(2);

            // Albums commonly have no overview, so DF-R4.4 only checks overviews on movies and series.
            if (item.Kind is ItemKind.Movie or ItemKind.Series && !item.HasOverview)
            {
                missing.Add("No overview");
            }

            if (!item.HasPrimaryImage)
            {
                missing.Add("No primary image");
            }

            if (missing.Count > 0)
            {
                findings.Add(Finding.From(item, CheckId.IncompleteMetadata, string.Join("; ", missing)));
            }
        }

        return findings;
    }
}
