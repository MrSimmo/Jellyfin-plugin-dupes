namespace Jellyfin.Plugin.DupeFinder.Model;

/// <summary>
/// A library offered for scanning (DF-R2.1).
/// </summary>
public sealed class LibraryDto
{
    /// <summary>
    /// Gets or sets the library id (32 hex digits).
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the library name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the Jellyfin collection type, if any.
    /// </summary>
    public string? CollectionType { get; set; }
}
