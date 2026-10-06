using System.Collections.Generic;
using Jellyfin.Plugin.DupeFinder.Model;

namespace Jellyfin.Plugin.DupeFinder.Scanning;

/// <summary>
/// Where scanned items come from; the seam that keeps ScanService testable without Jellyfin.
/// </summary>
internal interface ILibraryItemSource
{
    IReadOnlyList<LibraryInfo> GetLibraries();

    /// <summary>
    /// Returns items of the given kinds in one library, with <see cref="ScannedItem.LibraryName"/> set
    /// to that library. Extras and additional parts are excluded; alternate versions are included.
    /// </summary>
    /// <param name="library">The library to read.</param>
    /// <param name="kinds">The item kinds to return.</param>
    /// <returns>The matching items in that library.</returns>
    IReadOnlyList<ScannedItem> GetItems(LibraryInfo library, IReadOnlyCollection<ItemKind> kinds);
}
