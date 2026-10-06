using System.Collections.Generic;

namespace Jellyfin.Plugin.DupeFinder.Model;

/// <summary>
/// Body of <c>POST /DupeFinder/Scan</c> (DF-R2.2).
/// </summary>
public sealed class ScanRequest
{
    /// <summary>
    /// Gets or sets the ids of the libraries to scan.
    /// </summary>
    public IReadOnlyList<string>? LibraryIds { get; set; }

    /// <summary>
    /// Gets or sets the check ids to run.
    /// </summary>
    public IReadOnlyList<string>? Checks { get; set; }
}
