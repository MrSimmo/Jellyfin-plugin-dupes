using System;
using System.Linq;
using Jellyfin.Plugin.DupeFinder.Model;
using Jellyfin.Plugin.DupeFinder.Scanning;
using Xunit;

namespace Jellyfin.Plugin.DupeFinder.Tests;

public class DuplicateDetectorTests
{
    [Fact]
    public void Movies_SameTmdb_AreGrouped()
    {
        var a = TestItems.Movie("The Matrix", 1999, tmdb: "603");
        var b = TestItems.Movie("Matrix, The", 1999, tmdb: "603");

        var group = Assert.Single(DuplicateDetector.FindDuplicateMovies([a, b]));

        Assert.Equal(2, group.Count);
        Assert.All(group, f => Assert.Equal(CheckId.DuplicateMovies, f.Check));
        Assert.All(group, f => Assert.Equal("Same TMDb id 603", f.Reason));
        Assert.All(group, f => Assert.Null(f.Group));
    }

    [Fact]
    public void Movies_SameImdbDifferentCase_AreGrouped()
    {
        var a = TestItems.Movie("The Matrix", 1999, imdb: "tt0133093");
        var b = TestItems.Movie("The Matrix", 1999, imdb: "TT0133093");

        var group = Assert.Single(DuplicateDetector.FindDuplicateMovies([a, b]));

        Assert.Equal(2, group.Count);
        Assert.Contains(group, f => f.Reason == "Same IMDb id tt0133093");
        Assert.Contains(group, f => f.Reason == "Same IMDb id TT0133093");
    }

    [Fact]
    public void Movies_BothMatchedDifferentTmdb_SameTitleAndYear_AreNotGrouped()
    {
        var a = TestItems.Movie("Heat", 1995, tmdb: "949");
        var b = TestItems.Movie("Heat", 1995, tmdb: "12345");

        Assert.Empty(DuplicateDetector.FindDuplicateMovies([a, b]));
    }

    [Fact]
    public void Movies_MatchedAndUnmatched_SameTitleAndYear_AreGrouped()
    {
        var matched = TestItems.Movie("Heat", 1995, tmdb: "949");
        var unmatched = TestItems.Movie("Heat", 1995);

        var group = Assert.Single(DuplicateDetector.FindDuplicateMovies([matched, unmatched]));

        Assert.Equal(2, group.Count);
        Assert.All(group, f => Assert.Equal("Same title + year (unmatched)", f.Reason));
    }

    [Fact]
    public void Movies_UnmatchedAccentVariants_AreGrouped()
    {
        var a = TestItems.Movie("Amélie", 2001);
        var b = TestItems.Movie("Amelie", 2001);

        Assert.Equal(2, Assert.Single(DuplicateDetector.FindDuplicateMovies([a, b])).Count);
    }

    [Fact]
    public void Movies_UnmatchedDifferentYears_AreNotGrouped()
    {
        var a = TestItems.Movie("Solaris", 1972);
        var b = TestItems.Movie("Solaris", 2002);

        Assert.Empty(DuplicateDetector.FindDuplicateMovies([a, b]));
    }

    [Fact]
    public void Movies_ChainedByTmdbThenImdb_FormOneGroupOfThree()
    {
        var a = TestItems.Movie("Alien", 1979, tmdb: "348");
        var b = TestItems.Movie("Alien", 1979, tmdb: "348", imdb: "tt0078748");
        var c = TestItems.Movie("Alien (Director's Cut)", 2003, imdb: "tt0078748");

        var group = Assert.Single(DuplicateDetector.FindDuplicateMovies([a, b, c]));

        Assert.Equal(3, group.Count);
        Assert.Equal("Same TMDb id 348; Same IMDb id tt0078748", group.Single(f => f.ItemId == b.Id).Reason);
    }

    [Fact]
    public void Movies_UnmatchedWithBlankOrPunctuationNames_AreNeverGrouped()
    {
        var a = TestItems.Movie(string.Empty);
        var b = TestItems.Movie("!!!");
        var c = TestItems.Movie("???");

        Assert.Empty(DuplicateDetector.FindDuplicateMovies([a, b, c]));
    }

    [Fact]
    public void Movies_GroupMembers_AreOrderedByNameThenPath()
    {
        var b = TestItems.Movie("B", 2000, tmdb: "1");
        var a = TestItems.Movie("A", 2000, tmdb: "1");

        var group = Assert.Single(DuplicateDetector.FindDuplicateMovies([b, a]));

        Assert.Equal(new[] { "A", "B" }, group.Select(f => f.Name));
    }

    [Fact]
    public void Series_SameTvdb_AreGrouped()
    {
        var a = TestItems.Series("Lost", 2004, tvdb: "73739");
        var b = TestItems.Series("Lost (2004)", 2004, tvdb: "73739");

        var group = Assert.Single(DuplicateDetector.FindDuplicateSeries([a, b]));

        Assert.All(group, f => Assert.Equal("Same TVDb id 73739", f.Reason));
        Assert.All(group, f => Assert.Equal(CheckId.DuplicateSeries, f.Check));
    }

    [Fact]
    public void Episodes_SameSeriesSeasonEpisode_AreGrouped()
    {
        var series = Guid.NewGuid();
        var a = TestItems.Episode(series, 1, 3);
        var b = TestItems.Episode(series, 1, 3);

        var group = Assert.Single(DuplicateDetector.FindDuplicateEpisodes([a, b]));

        Assert.All(group, f => Assert.Equal("Same episode S01E03", f.Reason));
    }

    [Fact]
    public void Episodes_SameNumbersDifferentSeries_AreNotGrouped()
    {
        var a = TestItems.Episode(Guid.NewGuid(), 1, 3);
        var b = TestItems.Episode(Guid.NewGuid(), 1, 3);

        Assert.Empty(DuplicateDetector.FindDuplicateEpisodes([a, b]));
    }

    [Fact]
    public void Episodes_NullEpisodeNumber_AreNeverGrouped()
    {
        var series = Guid.NewGuid();
        var a = TestItems.Episode(series, 1, null);
        var b = TestItems.Episode(series, 1, null);

        Assert.Empty(DuplicateDetector.FindDuplicateEpisodes([a, b]));
    }

    [Fact]
    public void Albums_SameMusicBrainzRelease_AreGrouped()
    {
        var a = TestItems.Album("Abbey Road", ["The Beatles"], musicBrainzAlbum: "mb-1");
        var b = TestItems.Album("Abbey Road (Remastered)", ["The Beatles"], musicBrainzAlbum: "mb-1");

        var group = Assert.Single(DuplicateDetector.FindDuplicateAlbums([a, b]));

        Assert.All(group, f => Assert.Equal("Same MusicBrainz release mb-1", f.Reason));
    }

    [Fact]
    public void Albums_SameArtistAndNameDifferentRelease_AreGrouped()
    {
        var a = TestItems.Album("Abbey Road", ["The Beatles"], musicBrainzAlbum: "mb-1");
        var b = TestItems.Album("Abbey Road", ["The Beatles"], musicBrainzAlbum: "mb-2");

        var group = Assert.Single(DuplicateDetector.FindDuplicateAlbums([a, b]));

        Assert.All(group, f => Assert.Equal("Same album artist + album", f.Reason));
    }

    [Fact]
    public void Albums_SameNameDifferentArtist_AreNotGrouped()
    {
        var a = TestItems.Album("Greatest Hits", ["Queen"]);
        var b = TestItems.Album("Greatest Hits", ["ABBA"]);

        Assert.Empty(DuplicateDetector.FindDuplicateAlbums([a, b]));
    }

    [Fact]
    public void Albums_SameNameWithoutAlbumArtist_AreNotGrouped()
    {
        var a = TestItems.Album("Greatest Hits", []);
        var b = TestItems.Album("Greatest Hits", []);

        Assert.Empty(DuplicateDetector.FindDuplicateAlbums([a, b]));
    }
}
