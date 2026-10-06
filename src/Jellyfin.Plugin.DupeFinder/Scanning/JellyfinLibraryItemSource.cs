using System;
using System.Collections.Generic;
using System.Linq;
using Jellyfin.Data.Enums;
using Jellyfin.Plugin.DupeFinder.Model;
using MediaBrowser.Controller.Dto;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Audio;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Querying;

namespace Jellyfin.Plugin.DupeFinder.Scanning;

/// <summary>
/// The only code that reads Jellyfin's library (ADR-0002). Items are read-only snapshots and are never
/// saved back (DF-C3).
/// </summary>
internal sealed class JellyfinLibraryItemSource : ILibraryItemSource
{
    private readonly ILibraryManager _libraryManager;

    public JellyfinLibraryItemSource(ILibraryManager libraryManager)
    {
        _libraryManager = libraryManager;
    }

    public IReadOnlyList<LibraryInfo> GetLibraries()
    {
        var libraries = new List<LibraryInfo>();
        foreach (var folder in _libraryManager.GetVirtualFolders())
        {
            if (Guid.TryParse(folder.ItemId, out var id))
            {
                libraries.Add(new LibraryInfo { Id = id, Name = folder.Name, CollectionType = folder.CollectionType?.ToString() });
            }
        }

        return libraries;
    }

    public IReadOnlyList<ScannedItem> GetItems(LibraryInfo library, IReadOnlyCollection<ItemKind> kinds)
    {
        var items = new List<ScannedItem>();
        var itemKinds = kinds.Where(kind => kind != ItemKind.MusicArtist).Select(ToBaseItemKind).ToArray();
        if (itemKinds.Length > 0)
        {
            var query = new InternalItemsQuery
            {
                // LibraryManager.GetItemList turns a recursive ParentId on a library into its TopParentIds.
                // User stays null: no per-user grouping or access filtering for an admin scan.
                ParentId = library.Id,
                Recursive = true,
                IsVirtualItem = false,
                IncludeItemTypes = itemKinds,

                // ADR-0008: 12.2 stores auto-detected alternate versions as owned items, which the default
                // query drops; include owned items and filter extras and additional parts below.
                IncludeOwnedItems = true,
            };

            foreach (var item in _libraryManager.GetItemList(query))
            {
                if (!IsExtraOrPart(item))
                {
                    items.Add(Map(item, library.Name));
                }
            }
        }

        if (kinds.Contains(ItemKind.MusicArtist))
        {
            // DF-R4.3: same library scoping as Jellyfin's own Album Artists endpoint (ArtistsController).
            var artistQuery = new InternalItemsQuery
            {
                AncestorIds = [library.Id],
                DtoOptions = new DtoOptions(false) { Fields = [ItemFields.ProviderIds], EnableImages = false },
            };

            foreach (var (item, _) in _libraryManager.GetAlbumArtists(artistQuery).Items)
            {
                items.Add(Map(item, library.Name));
            }
        }

        return items;
    }

    private static bool IsExtraOrPart(BaseItem item)
        => item.ExtraType is not null
            || (!item.OwnerId.Equals(Guid.Empty) && item is not Video { PrimaryVersionId: not null });

    private static ScannedItem Map(BaseItem item, string libraryName)
    {
        // WHY: items loaded from the database get a case-sensitive ProviderIds dictionary (BaseItemMapper), so
        // keys differing only by case would make the case-insensitive copy constructor throw and fail the whole
        // scan. Blank values are skipped (spec §4) and the first non-blank value per key wins.
        var providerIds = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, value) in item.ProviderIds)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                providerIds.TryAdd(key, value);
            }
        }

        var episode = item as Episode;
        return new ScannedItem
        {
            Id = item.Id,
            Kind = ToItemKind(item),
            LibraryName = libraryName,
            Name = item.Name ?? string.Empty,
            Year = item.ProductionYear,
            Path = item.Path,
            SizeBytes = item.Size,
            ProviderIds = providerIds,
            PrimaryVersionId = (item as Video)?.PrimaryVersionId,
            SeriesId = episode is null || episode.SeriesId.Equals(Guid.Empty) ? null : episode.SeriesId,
            SeriesName = episode?.SeriesName,
            Season = episode?.ParentIndexNumber,
            Episode = episode?.IndexNumber,
            AlbumArtists = (item as MusicAlbum)?.AlbumArtists ?? [],
            HasOverview = !string.IsNullOrWhiteSpace(item.Overview),
            HasPrimaryImage = item.HasImage(ImageType.Primary, 0),
        };
    }

    private static ItemKind ToItemKind(BaseItem item) => item switch
    {
        Movie => ItemKind.Movie,
        Series => ItemKind.Series,
        Episode => ItemKind.Episode,
        MusicAlbum => ItemKind.MusicAlbum,
        MusicArtist => ItemKind.MusicArtist,
        _ => throw new InvalidOperationException("Unexpected item type " + item.GetType().Name),
    };

    private static BaseItemKind ToBaseItemKind(ItemKind kind) => kind switch
    {
        ItemKind.Movie => BaseItemKind.Movie,
        ItemKind.Series => BaseItemKind.Series,
        ItemKind.Episode => BaseItemKind.Episode,
        ItemKind.MusicAlbum => BaseItemKind.MusicAlbum,
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };
}
