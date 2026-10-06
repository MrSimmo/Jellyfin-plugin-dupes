using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Jellyfin.Plugin.DupeFinder.Model;

namespace Jellyfin.Plugin.DupeFinder.Scanning;

/// <summary>
/// Duplicate rules DF-R3.1–R3.4 with reasons per DF-R3.6. Callers pass the collapsed set (spec §4)
/// for movies and episodes.
/// </summary>
internal static class DuplicateDetector
{
    private const string MusicBrainzAlbumKey = "MusicBrainzAlbum";

    private static readonly (string Key, string Label)[] _movieKeys = [("Tmdb", "TMDb"), ("Imdb", "IMDb")];

    private static readonly (string Key, string Label)[] _seriesKeys = [("Tvdb", "TVDb"), ("Tmdb", "TMDb"), ("Imdb", "IMDb")];

    public static IReadOnlyList<IReadOnlyList<Finding>> FindDuplicateMovies(IReadOnlyList<ScannedItem> movies)
        => FindByProviderOrTitle(movies, _movieKeys, CheckId.DuplicateMovies);

    public static IReadOnlyList<IReadOnlyList<Finding>> FindDuplicateSeries(IReadOnlyList<ScannedItem> series)
        => FindByProviderOrTitle(series, _seriesKeys, CheckId.DuplicateSeries);

    public static IReadOnlyList<IReadOnlyList<Finding>> FindDuplicateEpisodes(IReadOnlyList<ScannedItem> episodes)
    {
        var builder = new GroupBuilder(episodes);
        builder.UnionBuckets(
            EpisodeKey,
            item => string.Create(CultureInfo.InvariantCulture, $"Same episode S{item.Season:00}E{item.Episode:00}"));
        return builder.BuildGroups(CheckId.DuplicateEpisodes);
    }

    public static IReadOnlyList<IReadOnlyList<Finding>> FindDuplicateAlbums(IReadOnlyList<ScannedItem> albums)
    {
        var builder = new GroupBuilder(albums);
        builder.UnionBuckets(
            item => item.GetProviderId(MusicBrainzAlbumKey)?.ToUpperInvariant(),
            item => "Same MusicBrainz release " + item.GetProviderId(MusicBrainzAlbumKey));
        builder.UnionBuckets(AlbumArtistAndNameKey, _ => "Same album artist + album");
        return builder.BuildGroups(CheckId.DuplicateAlbums);
    }

    private static IReadOnlyList<IReadOnlyList<Finding>> FindByProviderOrTitle(
        IReadOnlyList<ScannedItem> items,
        (string Key, string Label)[] providerKeys,
        CheckId check)
    {
        var builder = new GroupBuilder(items);
        foreach (var (key, label) in providerKeys)
        {
            builder.UnionBuckets(
                item => item.GetProviderId(key)?.ToUpperInvariant(),
                item => "Same " + label + " id " + item.GetProviderId(key));
        }

        // DF-R3.1(c): title + year only links a bucket containing an unmatched item, so two matched
        // films that share a title and year (remakes) are never grouped on name alone.
        builder.UnionBuckets(TitleYearKey, _ => "Same title + year (unmatched)", bucket => bucket.Any(item => !item.HasAnyProviderId));
        return builder.BuildGroups(check);
    }

    private static string? TitleYearKey(ScannedItem item)
    {
        var name = TextNormalizer.Normalize(item.Name);
        return name.Length == 0 ? null : string.Create(CultureInfo.InvariantCulture, $"{name}|{item.Year}");
    }

    private static string? EpisodeKey(ScannedItem item)
        => item.SeriesId is { } seriesId && item.Season is { } season && item.Episode is { } episode
            ? string.Create(CultureInfo.InvariantCulture, $"{seriesId:N}|{season}|{episode}")
            : null;

    private static string? AlbumArtistAndNameKey(ScannedItem item)
    {
        var artists = TextNormalizer.Normalize(string.Join(' ', item.AlbumArtists));
        var name = TextNormalizer.Normalize(item.Name);
        return artists.Length == 0 || name.Length == 0 ? null : artists + "|" + name;
    }
}
