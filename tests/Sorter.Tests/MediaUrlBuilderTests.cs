using Sorter.Data;

namespace Sorter.Tests;

public class MediaUrlBuilderTests
{
    // the paths don't have to exist, the builder works on the path text only
    private static readonly string s_root = Path.Combine(Path.GetTempPath(), "sorter-url-tests");
    private static readonly string s_source = Path.Combine(s_root, "Photos");
    private static readonly string s_destination = Path.Combine(s_root, "Photos-sorted");
    private readonly MediaUrlBuilder _builder = new(s_source, s_destination);

    private static string In(string directory, params string[] parts) => Path.Combine([directory, .. parts]);

    [Fact]
    public void SourceFile_GetsSourceUrl()
    {
        Assert.Equal("/src/a.jpg", _builder.GetUrl(In(s_source, "a.jpg")));
    }

    [Fact]
    public void DestinationFile_GetsDestinationUrl()
    {
        Assert.Equal("/dest/A/a.jpg", _builder.GetUrl(In(s_destination, "A", "a.jpg")));
    }

    [Fact]
    public void DestinationWhoseNameStartsWithSourceName_IsNotTreatedAsSource()
    {
        string url = _builder.GetUrl(In(s_destination, "a.jpg"));

        Assert.Equal("/dest/a.jpg", url);
        Assert.DoesNotContain("..", url);
    }

    [Fact]
    public void SpecialCharactersInEverySegment_AreEscaped()
    {
        string url = _builder.GetUrl(In(s_source, "sub folder #1", "zdjęcie 50%?.jpg"));

        Assert.Equal("/src/sub%20folder%20%231/zdj%C4%99cie%2050%25%3F.jpg", url);
    }

    [Fact]
    public void RootsWithTrailingSeparator_Work()
    {
        var builder = new MediaUrlBuilder(s_source + Path.DirectorySeparatorChar, s_destination + Path.DirectorySeparatorChar);

        Assert.Equal("/src/a.jpg", builder.GetUrl(In(s_source, "a.jpg")));
        Assert.Equal("/dest/a.jpg", builder.GetUrl(In(s_destination, "a.jpg")));
    }

    [Fact]
    public void DestinationNestedInSource_MapsToSource()
    {
        // allowed by the configuration dialog with a warning, the source endpoint can serve these files
        var builder = new MediaUrlBuilder(s_source, In(s_source, "sorted"));

        Assert.Equal("/src/sorted/A/a.jpg", builder.GetUrl(In(s_source, "sorted", "A", "a.jpg")));
    }

    [Fact]
    public void FileOutsideBothDirectories_ReturnsPhysicalPath()
    {
        string path = In(s_root, "Other", "a.jpg");

        Assert.Equal(path, _builder.GetUrl(path));
    }

    [Fact]
    public void EmptyBuilder_ReturnsPhysicalPath()
    {
        string path = In(s_source, "a.jpg");

        Assert.Equal(path, MediaUrlBuilder.Empty.GetUrl(path));
    }

    [Fact]
    public void TempUrl_UsesOnlyTheFileName()
    {
        Assert.Equal("/tmp/abc123.jpg", MediaUrlBuilder.GetTempUrl(In(s_root, "Temp", "abc123.jpg")));
    }
}
