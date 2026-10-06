using System;
using System.Collections.Generic;
using System.Linq;
using Jellyfin.Plugin.DupeFinder.Model;

namespace Jellyfin.Plugin.DupeFinder.Scanning;

/// <summary>
/// Spec §4 version groups: the collapsed set used by most checks, and the MergedVersions check (DF-R3.5).
/// </summary>
internal static class VersionGroups
{
    public static IReadOnlyList<ScannedItem> Collapse(IReadOnlyList<ScannedItem> items)
    {
        var ids = items.Select(item => item.Id).ToHashSet();

        // An alternate whose primary was not scanned stands in for its version group.
        return items.Where(item => item.PrimaryVersionId is not { } primaryId || !ids.Contains(primaryId)).ToList();
    }

    public static IReadOnlyList<IReadOnlyList<Finding>> FindMergedVersions(IReadOnlyList<ScannedItem> items)
    {
        var byId = items.ToDictionary(item => item.Id);
        var alternatesByPrimary = new Dictionary<Guid, List<ScannedItem>>();
        foreach (var item in items)
        {
            if (item.PrimaryVersionId is not { } primaryId || !byId.ContainsKey(primaryId))
            {
                continue;
            }

            if (!alternatesByPrimary.TryGetValue(primaryId, out var alternates))
            {
                alternates = [];
                alternatesByPrimary.Add(primaryId, alternates);
            }

            alternates.Add(item);
        }

        var groups = new List<IReadOnlyList<Finding>>();
        foreach (var (primaryId, alternates) in alternatesByPrimary)
        {
            var group = new List<Finding> { Finding.From(byId[primaryId], CheckId.MergedVersions, "Merged versions") };
            group.AddRange(alternates
                .OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
                .ThenBy(item => item.Path, StringComparer.OrdinalIgnoreCase)
                .Select(item => Finding.From(item, CheckId.MergedVersions, "Merged versions")));
            groups.Add(group);
        }

        return groups;
    }
}
