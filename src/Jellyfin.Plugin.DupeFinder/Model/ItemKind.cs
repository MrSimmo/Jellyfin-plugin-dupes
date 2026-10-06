namespace Jellyfin.Plugin.DupeFinder.Model;

/// <summary>
/// The item types the plugin scans (serialised as <c>ItemType</c>, DF-R2.3).
/// </summary>
public enum ItemKind
{
    /// <summary>
    /// A movie.
    /// </summary>
    Movie,

    /// <summary>
    /// A TV series.
    /// </summary>
    Series,

    /// <summary>
    /// A TV episode.
    /// </summary>
    Episode,

    /// <summary>
    /// A music album.
    /// </summary>
    MusicAlbum,

    /// <summary>
    /// A music album artist.
    /// </summary>
    MusicArtist
}
