using System;
using System.Collections.Generic;
using Jellyfin.Plugin.DupeFinder.Model;

namespace Jellyfin.Plugin.DupeFinder.Scanning;

/// <summary>
/// Jellyfin-free snapshot of one library item, so detection rules can be unit-tested (ADR-0002).
/// </summary>
internal sealed record ScannedItem
{
    public required Guid Id { get; init; }

    public required ItemKind Kind { get; init; }

    public required string LibraryName { get; init; }

    public required string Name { get; init; }

    public int? Year { get; init; }

    public string? Path { get; init; }

    public long? SizeBytes { get; init; }

    public IReadOnlyDictionary<string, string> ProviderIds { get; init; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    public Guid? PrimaryVersionId { get; init; }

    public Guid? SeriesId { get; init; }

    public string? SeriesName { get; init; }

    public int? Season { get; init; }

    public int? Episode { get; init; }

    public IReadOnlyList<string> AlbumArtists { get; init; } = [];

    public bool HasOverview { get; init; }

    public bool HasPrimaryImage { get; init; }

    // Spec §4 "has provider id": blank values do not count.
    public bool HasAnyProviderId
    {
        get
        {
            foreach (var value in ProviderIds.Values)
            {
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return true;
                }
            }

            return false;
        }
    }

    public string? GetProviderId(string key)
        => ProviderIds.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value) ? value.Trim() : null;
}
