using System;
using System.Collections.Generic;
using Jellyfin.Plugin.DupeFinder.Model;
using Jellyfin.Plugin.DupeFinder.Scanning;

namespace Jellyfin.Plugin.DupeFinder.Tests;

internal static class TestItems
{
    public static ScannedItem Movie(string name, int? year = null, string? tmdb = null, string? imdb = null, Guid? primaryVersionId = null)
        => Create(ItemKind.Movie, name, year, Ids(("Tmdb", tmdb), ("Imdb", imdb))) with { PrimaryVersionId = primaryVersionId };

    public static ScannedItem Series(string name, int? year = null, string? tvdb = null, string? tmdb = null)
        => Create(ItemKind.Series, name, year, Ids(("Tvdb", tvdb), ("Tmdb", tmdb)));

    public static ScannedItem Episode(Guid seriesId, int? season, int? episode, string? tvdb = null, Guid? primaryVersionId = null)
        => Create(ItemKind.Episode, "Pilot", null, Ids(("Tvdb", tvdb))) with
        {
            SeriesId = seriesId,
            SeriesName = "Show",
            Season = season,
            Episode = episode,
            PrimaryVersionId = primaryVersionId,
        };

    public static ScannedItem Album(string name, string[] albumArtists, string? musicBrainzAlbum = null)
        => Create(ItemKind.MusicAlbum, name, null, Ids(("MusicBrainzAlbum", musicBrainzAlbum))) with { AlbumArtists = albumArtists };

    public static ScannedItem Artist(string name, string? musicBrainzArtist = null)
        => Create(ItemKind.MusicArtist, name, null, Ids(("MusicBrainzArtist", musicBrainzArtist)));

    public static ScannedItem WithIds(ScannedItem item, params (string Key, string? Value)[] pairs)
        => item with { ProviderIds = Ids(pairs) };

    private static ScannedItem Create(ItemKind kind, string name, int? year, Dictionary<string, string> ids)
        => new()
        {
            Id = Guid.NewGuid(),
            Kind = kind,
            LibraryName = "Library",
            Name = name,
            Year = year,
            Path = "/media/" + name,
            SizeBytes = 1000,
            ProviderIds = ids,
            HasOverview = true,
            HasPrimaryImage = true,
        };

    private static Dictionary<string, string> Ids(params (string Key, string? Value)[] pairs)
    {
        var ids = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, value) in pairs)
        {
            if (value is not null)
            {
                ids[key] = value;
            }
        }

        return ids;
    }
}
