using Sorter.Data;

namespace Sorter.Tests;

public class FileFilterTests
{
    [Theory]
    [InlineData("jpg")]
    [InlineData("MP4")]
    [InlineData("")]
    public void AllowAll_AllowsAnyExtension(string extension)
    {
        Assert.True(FileFilter.AllowAll.IsAllowed(extension));
    }

    [Theory]
    [InlineData("ds_store")]
    [InlineData("DS_Store")]
    public void DsStore_IsAlwaysExcluded(string extension)
    {
        Assert.False(FileFilter.AllowAll.IsAllowed(extension));
        Assert.False(new FileFilter(true, ["ds_store"], []).IsAllowed(extension));
    }

    [Theory]
    [InlineData("mp4", false)]
    [InlineData("MP4", false)]
    [InlineData("jpg", true)]
    [InlineData("", true)]
    public void BlackList_ExcludesListedExtensionsIgnoringCase(string extension, bool expected)
    {
        var filter = new FileFilter(false, ["txt"], ["Mp4"]);

        Assert.Equal(expected, filter.IsAllowed(extension));
    }

    [Theory]
    [InlineData("txt", true)]
    [InlineData("TXT", true)]
    [InlineData("jpg", false)]
    [InlineData("", false)]
    public void WhiteList_AllowsOnlyListedExtensionsIgnoringCase(string extension, bool expected)
    {
        var filter = new FileFilter(true, ["Txt"], ["txt"]);

        Assert.Equal(expected, filter.IsAllowed(extension));
    }

    [Fact]
    public void EmptyWhiteList_AllowsNothing()
    {
        Assert.False(new FileFilter(true, [], []).IsAllowed("jpg"));
    }

    [Theory]
    [InlineData("jpg", "jpg")]
    [InlineData(".jpg", "jpg")]
    [InlineData("  .JPG ", "JPG")]
    [InlineData("tar.gz", "tar.gz")]
    [InlineData(".", "")]
    [InlineData("", "")]
    [InlineData(null, "")]
    public void NormalizeExtension_DropsSurroundingSpacesAndLeadingDot(string? input, string expected)
    {
        Assert.Equal(expected, FileFilter.NormalizeExtension(input));
    }

    [Fact]
    public void EntriesWithLeadingDot_StillMatch()
    {
        // e.g. entered before the configuration dialog normalized them
        Assert.False(new FileFilter(false, [], [".mp4"]).IsAllowed("mp4"));
        Assert.True(new FileFilter(true, [" .TXT"], []).IsAllowed("txt"));
    }
}
