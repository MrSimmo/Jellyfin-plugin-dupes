using System;
using System.Globalization;
using System.Linq;
using Jellyfin.Plugin.DupeFinder.Model;
using Jellyfin.Plugin.DupeFinder.Scanning;
using Xunit;

namespace Jellyfin.Plugin.DupeFinder.Tests;

public class ScanServiceTests
{
    [Fact]
    public void Scan_WithoutLibraries_IsRejected()
    {
        var outcome = new ScanService(new FakeLibraryItemSource()).Scan(new ScanRequest { LibraryIds = [], Checks = ["DuplicateMovies"] });

        Assert.Equal("Select at least one library.", outcome.Error);
        Assert.Null(outcome.Response);
    }

    [Fact]
    public void Scan_WithUnparseableOrUnknownLibrary_IsRejected()
    {
        var service = new ScanService(new FakeLibraryItemSource());
        var unknown = Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture);

        Assert.Equal("Unknown library id 'nope'.", service.Scan(new ScanRequest { LibraryIds = ["nope"], Checks = ["DuplicateMovies"] }).Error);
        Assert.Equal($"Unknown library id '{unknown}'.", service.Scan(new ScanRequest { LibraryIds = [unknown], Checks = ["DuplicateMovies"] }).Error);
    }

    [Fact]
    public void Scan_WithoutChecks_IsRejected()
    {
        var source = new FakeLibraryItemSource();
        var films = source.Add("Films");

        Assert.Equal("Select at least one check.", new ScanService(source).Scan(new ScanRequest { LibraryIds = [Id(films)], Checks = [] }).Error);
    }

    [Fact]
    public void Scan_WithUnknownCheck_IsRejected()
    {
        var source = new FakeLibraryItemSource();
        var films = source.Add("Films");

        Assert.Equal("Unknown check 'Bogus'.", new ScanService(source).Scan(new ScanRequest { LibraryIds = [Id(films)], Checks = ["Bogus"] }).Error);
        Assert.Equal("Unknown check '3'.", new ScanService(source).Scan(new ScanRequest { LibraryIds = [Id(films)], Checks = ["3"] }).Error);
    }

    [Fact]
    public void Scan_VersionGroupPlusSeparateCopy_ReportsDuplicateAndMergedGroups()
    {
        var primary = TestItems.Movie("Heat", 1995, tmdb: "949");
        var alternate = TestItems.Movie("Heat", 1995, tmdb: "949", primaryVersionId: primary.Id);
        var copy = TestItems.Movie("Heat", 1995, tmdb: "949");
        var source = new FakeLibraryItemSource();
        var films = source.Add("Films", primary, alternate);
        var more = source.Add("More Films", copy);

        var response = Scan(source, [films, more], "DuplicateMovies", "MergedVersions");

        var duplicates = response.Findings.Where(f => f.Check == CheckId.DuplicateMovies).Select(f => f.ItemId).ToList();
        Assert.Equal(2, duplicates.Count);
        Assert.Contains(primary.Id, duplicates);
        Assert.Contains(copy.Id, duplicates);
        var merged = response.Findings.Where(f => f.Check == CheckId.MergedVersions).Select(f => f.ItemId).ToList();
        Assert.Equal(new[] { primary.Id, alternate.Id }, merged);
        Assert.Equal(1, response.Summary["DuplicateMovies"]);
        Assert.Equal(1, response.Summary["MergedVersions"]);
    }

    [Fact]
    public void Scan_OrdersByCheckThenGroup_AndNumbersGroupsAcrossTheResponse()
    {
        var source = new FakeLibraryItemSource();
        var library = source.Add(
            "Mixed",
            TestItems.Movie("Brazil", 1985, tmdb: "68"),
            TestItems.Movie("Alien", 1979, tmdb: "348"),
            TestItems.Movie("Brazil", 1985, tmdb: "68"),
            TestItems.Movie("Alien", 1979, tmdb: "348"),
            TestItems.Series("Lost", 2004, tvdb: "73739"),
            TestItems.Series("Lost", 2004, tvdb: "73739"),
            TestItems.Movie("Zardoz", 1974));

        var response = Scan(source, [library], "UnmatchedMoviesSeries", "DuplicateSeries", "DuplicateMovies");

        Assert.Equal(new[] { "Alien", "Alien", "Brazil", "Brazil", "Lost", "Lost", "Zardoz" }, response.Findings.Select(f => f.Name));
        Assert.Equal(new int?[] { 1, 1, 2, 2, 3, 3, null }, response.Findings.Select(f => f.Group));
        Assert.Equal(2, response.Summary["DuplicateMovies"]);
        Assert.Equal(1, response.Summary["DuplicateSeries"]);
        Assert.Equal(1, response.Summary["UnmatchedMoviesSeries"]);
    }

    [Fact]
    public void Scan_ItemFoundThroughTwoLibraries_IsReportedOnce()
    {
        var artist = TestItems.Artist("Unknown Artist");
        var source = new FakeLibraryItemSource();
        var musicB = source.Add("Music B", artist);
        var musicA = source.Add("Music A", artist);

        var response = Scan(source, [musicB, musicA], "UnmatchedMusic");

        var finding = Assert.Single(response.Findings);
        Assert.Equal("Music A", finding.LibraryName);
        Assert.Equal(1, response.ScannedItemCount);
    }

    [Fact]
    public void Scan_SummaryListsEveryRequestedCheck_EvenWhenZero()
    {
        var source = new FakeLibraryItemSource();
        var films = source.Add("Films", TestItems.Movie("Heat", 1995, tmdb: "949"));

        var response = Scan(source, [films], "DuplicateAlbums", "UnmatchedEpisodes");

        Assert.Empty(response.Findings);
        Assert.Equal(0, response.Summary["DuplicateAlbums"]);
        Assert.Equal(0, response.Summary["UnmatchedEpisodes"]);
        Assert.Equal(2, response.Summary.Count);
    }

    [Fact]
    public void GetLibraries_AreOrderedByName()
    {
        var source = new FakeLibraryItemSource();
        source.Add("TV");
        source.Add("Films");

        Assert.Equal(new[] { "Films", "TV" }, new ScanService(source).GetLibraries().Select(l => l.Name));
    }

    private static string Id(LibraryInfo library) => library.Id.ToString("N", CultureInfo.InvariantCulture);

    private static ScanResponse Scan(FakeLibraryItemSource source, LibraryInfo[] libraries, params string[] checks)
    {
        var outcome = new ScanService(source).Scan(new ScanRequest { LibraryIds = libraries.Select(Id).ToList(), Checks = checks });
        Assert.Null(outcome.Error);
        return Assert.IsType<ScanResponse>(outcome.Response);
    }
}
