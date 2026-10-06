namespace Jellyfin.Plugin.DupeFinder.Model;

/// <summary>
/// The checks a scan can run. Declaration order is the display order (DF-R2.2).
/// </summary>
public enum CheckId
{
    /// <summary>
    /// Duplicate movies (DF-R3.1).
    /// </summary>
    DuplicateMovies,

    /// <summary>
    /// Duplicate series (DF-R3.2).
    /// </summary>
    DuplicateSeries,

    /// <summary>
    /// Duplicate episodes (DF-R3.3).
    /// </summary>
    DuplicateEpisodes,

    /// <summary>
    /// Duplicate music albums (DF-R3.4).
    /// </summary>
    DuplicateAlbums,

    /// <summary>
    /// Items Jellyfin has merged into one item with several versions (DF-R3.5).
    /// </summary>
    MergedVersions,

    /// <summary>
    /// Movies and series with no provider id (DF-R4.1).
    /// </summary>
    UnmatchedMoviesSeries,

    /// <summary>
    /// Episodes with no provider id (DF-R4.2).
    /// </summary>
    UnmatchedEpisodes,

    /// <summary>
    /// Music albums and album artists with no provider id (DF-R4.3).
    /// </summary>
    UnmatchedMusic,

    /// <summary>
    /// Matched items missing an overview or primary image (DF-R4.4).
    /// </summary>
    IncompleteMetadata
}
