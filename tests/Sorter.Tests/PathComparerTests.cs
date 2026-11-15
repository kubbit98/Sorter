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

    [Theory]
    [InlineData("photos", "photos", true)]
    [InlineData("photos", "photos/", true)]
    [InlineData("photos", "other/../photos", true)]
    [InlineData("photos", "photos-sorted", false)]
    [InlineData("photos", "photos/sub", false)]
    [InlineData("", "photos", false)]
    public void IsSamePath_NormalizesWithoutTouchingDisk(string path1, string path2, bool expected)
    {
        string Native(string p) => p.Replace('/', Path.DirectorySeparatorChar);

        Assert.Equal(expected, PathComparer.IsSamePath(Native(path1), Native(path2)));
    }

    [Theory]
    [InlineData("photos", "photos/a.jpg", true)]
    [InlineData("photos", "photos/2024/summer/a.jpg", true)]
    [InlineData("photos/", "photos/a.jpg", true)]
    [InlineData("photos", "photos-sorted/a.jpg", false)]
    [InlineData("photos", "photos", false)]
    [InlineData("photos/2024", "photos/a.jpg", false)]
    [InlineData("", "photos/a.jpg", false)]
    public void IsInside_ComparesWholePathSegmentsWithoutTouchingDisk(string directory, string path, bool expected)
    {
        // relative paths resolve against the current directory and don't exist, which must not matter
        string Native(string p) => p.Replace('/', Path.DirectorySeparatorChar);

        Assert.Equal(expected, PathComparer.IsInside(Native(directory), Native(path)));
    }

    [Fact]
    public void SameDirectory_IsNotReportedAsNested()
    {
        string photos = CreateDirectory("photos");

        Assert.False(PathComparer.AreNested(photos, photos));
    }
}
