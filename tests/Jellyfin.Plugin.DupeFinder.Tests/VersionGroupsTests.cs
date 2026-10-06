using System;
using System.Linq;
using Jellyfin.Plugin.DupeFinder.Model;
using Jellyfin.Plugin.DupeFinder.Scanning;
using Xunit;

namespace Jellyfin.Plugin.DupeFinder.Tests;

public class VersionGroupsTests
{
    [Fact]
    public void Collapse_DropsAlternateWhosePrimaryIsPresent()
    {
        var primary = TestItems.Movie("Heat", 1995, tmdb: "949");
        var alternate = TestItems.Movie("Heat", 1995, tmdb: "949", primaryVersionId: primary.Id);

        Assert.Same(primary, Assert.Single(VersionGroups.Collapse([primary, alternate])));
    }

    [Fact]
    public void Collapse_KeepsAlternateWhosePrimaryIsAbsent()
    {
        var orphan = TestItems.Movie("Heat", 1995, primaryVersionId: Guid.NewGuid());

        Assert.Same(orphan, Assert.Single(VersionGroups.Collapse([orphan])));
    }

    [Fact]
    public void MergedVersions_ListPrimaryFirst()
    {
        var alternateA = TestItems.Movie("A Heat", 1995);
        var primary = TestItems.Movie("Heat", 1995);
        alternateA = alternateA with { PrimaryVersionId = primary.Id };
        var alternateB = TestItems.Movie("B Heat", 1995, primaryVersionId: primary.Id);

        var group = Assert.Single(VersionGroups.FindMergedVersions([alternateB, alternateA, primary]));

        Assert.Equal(new[] { primary.Id, alternateA.Id, alternateB.Id }, group.Select(f => f.ItemId));
        Assert.All(group, f => Assert.Equal("Merged versions", f.Reason));
        Assert.All(group, f => Assert.Equal(CheckId.MergedVersions, f.Check));
    }

    [Fact]
    public void MergedVersions_IgnoreItemsWithoutAlternates()
    {
        Assert.Empty(VersionGroups.FindMergedVersions([TestItems.Movie("Heat", 1995), TestItems.Movie("Heat", 1995)]));
    }
}
