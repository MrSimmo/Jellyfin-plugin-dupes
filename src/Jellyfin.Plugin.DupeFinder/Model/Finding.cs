using System;
using System.Collections.Generic;
using System.Linq;
using Jellyfin.Plugin.DupeFinder.Scanning;

namespace Jellyfin.Plugin.DupeFinder.Model;

/// <summary>
/// One row of scan results (DF-R2.3).
/// </summary>
public sealed record Finding
{
    /// <summary>
    /// Gets or sets the check that produced this finding.
    /// </summary>
    public CheckId Check { get; set; }

    /// <summary>
    /// Gets or sets the duplicate group number, or null for single-item checks.
    /// </summary>
    public int? Group { get; set; }

    /// <summary>
    /// Gets or sets the Jellyfin item id.
    /// </summary>
    public Guid ItemId { get; set; }

    /// <summary>
    /// Gets or sets the name of the first scanned library the item was found in.
    /// </summary>
    public string LibraryName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the item type.
    /// </summary>
    public ItemKind ItemType { get; set; }

    /// <summary>
    /// Gets or sets the item name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the production year.
    /// </summary>
    public int? Year { get; set; }

    /// <summary>
    /// Gets or sets the season number (episodes only).
    /// </summary>
    public int? Season { get; set; }

    /// <summary>
    /// Gets or sets the episode number (episodes only).
    /// </summary>
    public int? Episode { get; set; }

    /// <summary>
    /// Gets or sets the series name (episodes only).
    /// </summary>
    public string? SeriesName { get; set; }

    /// <summary>
    /// Gets or sets the file or folder path.
    /// </summary>
    public string? Path { get; set; }

    /// <summary>
    /// Gets or sets the size in bytes.
    /// </summary>
    public long? SizeBytes { get; set; }

    /// <summary>
    /// Gets or sets the non-blank provider ids.
    /// </summary>
    public IReadOnlyDictionary<string, string> ProviderIds { get; set; } = new Dictionary<string, string>();

    /// <summary>
    /// Gets or sets why the item was reported (DF-R3.6, DF-R4).
    /// </summary>
    public string Reason { get; set; } = string.Empty;

    internal static Finding From(ScannedItem item, CheckId check, string reason)
        => new()
        {
            Check = check,
            ItemId = item.Id,
            LibraryName = item.LibraryName,
            ItemType = item.Kind,
            Name = item.Name,
            Year = item.Year,
            Season = item.Season,
            Episode = item.Episode,
            SeriesName = item.SeriesName,
            Path = item.Path,
            SizeBytes = item.SizeBytes,
            ProviderIds = item.ProviderIds
                .Where(pair => !string.IsNullOrWhiteSpace(pair.Value))
                .ToDictionary(pair => pair.Key, pair => pair.Value.Trim(), StringComparer.OrdinalIgnoreCase),
            Reason = reason,
        };
}
