using System;
using System.Collections.Generic;
using System.Linq;
using Jellyfin.Plugin.DupeFinder.Model;

namespace Jellyfin.Plugin.DupeFinder.Scanning;

/// <summary>
/// Items sharing a qualifying bucket key are unioned into one transitive group; each item keeps the
/// distinct reasons that put it there, in rule order (DF-R3, DF-R3.6).
/// </summary>
internal sealed class GroupBuilder
{
    private readonly IReadOnlyList<ScannedItem> _items;
    private readonly DisjointSet _set;
    private readonly List<string>[] _reasons;

    public GroupBuilder(IReadOnlyList<ScannedItem> items)
    {
        _items = items;
        _set = new DisjointSet(items.Count);
        _reasons = new List<string>[items.Count];
        for (var i = 0; i < items.Count; i++)
        {
            _reasons[i] = [];
        }
    }

    public void UnionBuckets(
        Func<ScannedItem, string?> keySelector,
        Func<ScannedItem, string> reasonSelector,
        Func<IReadOnlyList<ScannedItem>, bool>? bucketQualifies = null)
    {
        var buckets = new Dictionary<string, List<int>>(StringComparer.Ordinal);
        for (var i = 0; i < _items.Count; i++)
        {
            var key = keySelector(_items[i]);
            if (string.IsNullOrEmpty(key))
            {
                continue;
            }

            if (!buckets.TryGetValue(key, out var members))
            {
                members = [];
                buckets.Add(key, members);
            }

            members.Add(i);
        }

        foreach (var members in buckets.Values)
        {
            if (members.Count < 2 || (bucketQualifies is not null && !bucketQualifies(members.ConvertAll(i => _items[i]))))
            {
                continue;
            }

            foreach (var index in members)
            {
                _set.Union(members[0], index);
                var reason = reasonSelector(_items[index]);
                if (!_reasons[index].Contains(reason))
                {
                    _reasons[index].Add(reason);
                }
            }
        }
    }

    public IReadOnlyList<IReadOnlyList<Finding>> BuildGroups(CheckId check)
    {
        var groups = new Dictionary<int, List<int>>();
        for (var i = 0; i < _items.Count; i++)
        {
            // An item with no reason never joined a bucket of two or more, so it is not a duplicate.
            if (_reasons[i].Count == 0)
            {
                continue;
            }

            var root = _set.Find(i);
            if (!groups.TryGetValue(root, out var members))
            {
                members = [];
                groups.Add(root, members);
            }

            members.Add(i);
        }

        return groups.Values
            .Select(members => (IReadOnlyList<Finding>)members
                .Select(i => Finding.From(_items[i], check, string.Join("; ", _reasons[i])))
                .OrderBy(finding => finding.Name, StringComparer.OrdinalIgnoreCase)
                .ThenBy(finding => finding.Path, StringComparer.OrdinalIgnoreCase)
                .ToList())
            .ToList();
    }
}
