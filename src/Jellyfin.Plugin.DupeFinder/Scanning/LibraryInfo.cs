using System;

namespace Jellyfin.Plugin.DupeFinder.Scanning;

/// <summary>
/// Jellyfin-free description of one library.
/// </summary>
internal sealed record LibraryInfo
{
    public required Guid Id { get; init; }

    public required string Name { get; init; }

    public string? CollectionType { get; init; }
}
