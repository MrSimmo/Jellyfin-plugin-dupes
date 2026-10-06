using System.Collections.Generic;

namespace Jellyfin.Plugin.DupeFinder.Model;

/// <summary>
/// Result of <c>POST /DupeFinder/Scan</c> (DF-R2.3).
/// </summary>
public sealed class ScanResponse
{
    /// <summary>
    /// Gets or sets the number of distinct items read from the selected libraries.
    /// </summary>
    public int ScannedItemCount { get; set; }

    /// <summary>
    /// Gets or sets how long the scan took, in milliseconds.
    /// </summary>
    public long DurationMs { get; set; }

    /// <summary>
    /// Gets or sets the count per requested check: groups for duplicate checks, findings otherwise.
    /// </summary>
    public IReadOnlyDictionary<string, int> Summary { get; set; } = new Dictionary<string, int>();

    /// <summary>
    /// Gets or sets the findings, ordered by check, group, name and path.
    /// </summary>
    public IReadOnlyList<Finding> Findings { get; set; } = [];
}
