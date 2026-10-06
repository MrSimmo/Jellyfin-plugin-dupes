using Jellyfin.Plugin.DupeFinder.Scanning;
using Xunit;

namespace Jellyfin.Plugin.DupeFinder.Tests;

public class TextNormalizerTests
{
    [Theory]
    [InlineData("Amélie", "amelie")]
    [InlineData("Spider-Man: Far From Home", "spidermanfarfromhome")]
    [InlineData("Fast & Furious", "fastandfurious")]
    [InlineData("Fast and Furious", "fastandfurious")]
    [InlineData("Fast ＆ Furious", "fastandfurious")]
    [InlineData("  The   Matrix ", "thematrix")]
    [InlineData("ＡＢＣ", "abc")]
    [InlineData("千と千尋の神隠し", "千と千尋の神隠し")]
    [InlineData("𠮷野家", "𠮷野家")]
    [InlineData("!!!", "")]
    [InlineData("", "")]
    [InlineData(null, "")]
    public void Normalize_FollowsSpecDefinition(string? input, string expected)
    {
        Assert.Equal(expected, TextNormalizer.Normalize(input));
    }
}
