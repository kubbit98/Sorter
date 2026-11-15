using Sorter.Data;

namespace Sorter.Tests;

public class PathComparerTests : IDisposable
{
    private readonly TempDirectory _root = new();

    public void Dispose() => _root.Dispose();

    private string CreateDirectory(params string[] parts) => Directory.CreateDirectory(_root.Combine(parts)).FullName;

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void EmptyPath_NeverMatchesInsteadOfThrowing(string? empty)
    {
        string existing = CreateDirectory("photos");

        Assert.False(PathComparer.AreSameDirectory(existing, empty));
        Assert.False(PathComparer.AreSameDirectory(empty, existing));
        Assert.False(PathComparer.AreNested(existing, empty));
        Assert.False(PathComparer.AreNested(empty, empty));
    }

    [Fact]
    public void MissingDirectory_NeverMatches()
    {
        string missing = _root.Combine("missing");

        Assert.False(PathComparer.AreSameDirectory(missing, missing));
    }

    [Fact]
    public void SameDirectory_MatchesWithTrailingSeparatorOrRelativeSegments()
    {
        string photos = CreateDirectory("photos");
        CreateDirectory("other");

        Assert.True(PathComparer.AreSameDirectory(photos, photos + Path.DirectorySeparatorChar));
        Assert.True(PathComparer.AreSameDirectory(photos, Path.Combine(_root.Path, "other", "..", "photos")));
        Assert.False(PathComparer.AreSameDirectory(photos, _root.Combine("other")));
    }

    [Fact]
    public void NestedDirectories_AreDetectedInBothDirections()
    {
        string parent = CreateDirectory("photos");
        string child = CreateDirectory("photos", "2024", "summer");

        Assert.True(PathComparer.AreNested(parent, child));
        Assert.True(PathComparer.AreNested(child, parent));
    }

    [Fact]
    public void DirectoryWithCommonNamePrefix_IsNotNested()
    {
        string first = CreateDirectory("photos", "2024");
        string second = CreateDirectory("photos", "2024a");

        Assert.False(PathComparer.AreNested(first, second));
    }

    [Fact]
    public void SameDirectory_IsNotReportedAsNested()
    {
        string photos = CreateDirectory("photos");

        Assert.False(PathComparer.AreNested(photos, photos));
    }
}
