using Jellyfin.Plugin.DupeFinder.Model;
using Jellyfin.Plugin.DupeFinder.Scanning;
using Xunit;

namespace Jellyfin.Plugin.DupeFinder.Tests;

public class UnmatchedDetectorTests
{
    [Fact]
    public void Unmatched_OnlyBlankProviderValues_IsReported()
    {
        var item = TestItems.WithIds(TestItems.Movie("Heat", 1995), ("Tmdb", " "), ("Imdb", string.Empty));

        var finding = Assert.Single(UnmatchedDetector.FindUnmatched([item], CheckId.UnmatchedMoviesSeries));

        Assert.Equal("No provider ids", finding.Reason);
        Assert.Equal(CheckId.UnmatchedMoviesSeries, finding.Check);
        Assert.Null(finding.Group);
        Assert.Empty(finding.ProviderIds);
    }

    [Fact]
    public void Unmatched_WithAnyProviderId_IsNotReported()
    {
        Assert.Empty(UnmatchedDetector.FindUnmatched([TestItems.Movie("Heat", 1995, imdb: "tt0113277")], CheckId.UnmatchedMoviesSeries));
    }

    [Fact]
    public void Incomplete_MatchedMovieWithoutOverview_ReportsNoOverview()
    {
        var item = TestItems.Movie("Heat", 1995, tmdb: "949") with { HasOverview = false };

        Assert.Equal("No overview", Assert.Single(UnmatchedDetector.FindIncomplete([item])).Reason);
    }

    [Fact]
    public void Incomplete_MatchedSeriesMissingBoth_ListsBothInOrder()
    {
        var item = TestItems.Series("Lost", 2004, tvdb: "73739") with { HasOverview = false, HasPrimaryImage = false };

        var finding = Assert.Single(UnmatchedDetector.FindIncomplete([item]));

        Assert.Equal("No overview; No primary image", finding.Reason);
        Assert.Equal(CheckId.IncompleteMetadata, finding.Check);
    }

    [Fact]
    public void Incomplete_UnmatchedMovie_IsNotReported()
    {
        var item = TestItems.Movie("Heat", 1995) with { HasOverview = false, HasPrimaryImage = false };

        Assert.Empty(UnmatchedDetector.FindIncomplete([item]));
    }

    [Fact]
    public void Incomplete_AlbumWithoutOverview_IsNotReported()
    {
        var item = TestItems.Album("Abbey Road", ["The Beatles"], musicBrainzAlbum: "mb-1") with { HasOverview = false };

        Assert.Empty(UnmatchedDetector.FindIncomplete([item]));
    }

    [Fact]
    public void Incomplete_AlbumWithoutImage_ReportsNoPrimaryImage()
    {
        var item = TestItems.Album("Abbey Road", ["The Beatles"], musicBrainzAlbum: "mb-1") with { HasPrimaryImage = false };

        Assert.Equal("No primary image", Assert.Single(UnmatchedDetector.FindIncomplete([item])).Reason);
    }
}
