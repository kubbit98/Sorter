using Microsoft.Extensions.Logging;
using Sorter.Data;

namespace Sorter.Tests;

public class FileScannerTests : IDisposable
{
    private readonly TempDirectory _root = new();
    private readonly ListLogger<FileScanner> _logger = new();
    private readonly FileScanner _scanner;

    public FileScannerTests()
    {
        _scanner = new FileScanner(_logger);
    }

    public void Dispose() => _root.Dispose();

    private string Dir(params string[] parts) => _root.Combine(parts);

    private List<string> ScanNames(string directory, IReadOnlyCollection<string>? excluded = null, FileFilter? filter = null) =>
        _scanner.ScanFiles(directory, excluded ?? [], filter ?? FileFilter.AllowAll).Select(f => f.NameWithExtension).ToList();

    [Fact]
    public void MissingSource_ReturnsNoFiles()
    {
        Assert.Empty(ScanNames(Dir("missing")));
        Assert.Empty(_logger.Entries);
    }

    [Fact]
    public void Files_ComeBeforeSubfolders_AllOrderedByPath()
    {
        _root.CreateFile("src/b.txt");
        _root.CreateFile("src/a.txt");
        _root.CreateFile("src/sub2/d.txt");
        _root.CreateFile("src/sub1/c.txt");
        _root.CreateFile("src/sub1/deeper/e.txt");

        Assert.Equal(["a.txt", "b.txt", "c.txt", "e.txt", "d.txt"], ScanNames(Dir("src")));
    }

    [Fact]
    public void FileNameAndExtension_KeepOriginalCase()
    {
        _root.CreateFile("src/IMG_0001.JPG");

        var file = Assert.Single(_scanner.ScanFiles(Dir("src"), [], FileFilter.AllowAll));

        Assert.Equal("IMG_0001", file.Name);
        Assert.Equal("JPG", file.Extension);
        Assert.Equal(Dir("src", "IMG_0001.JPG"), file.PhysicalPath);
    }

    [Fact]
    public void FilesWithoutNameOrExtension_AreListed()
    {
        _root.CreateFile("src/README");
        _root.CreateFile("src/.txt");

        var files = _scanner.ScanFiles(Dir("src"), [], FileFilter.AllowAll);

        Assert.Contains(files, f => f.Name == "README" && f.Extension == "");
        Assert.Contains(files, f => f.Name == "" && f.Extension == "txt");
    }

    [Fact]
    public void Filter_IsApplied()
    {
        _root.CreateFile("src/a.mp4");
        _root.CreateFile("src/sub/b.txt");

        Assert.Equal(["b.txt"], ScanNames(Dir("src"), filter: new FileFilter(false, [], ["mp4"])));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ExcludedDirectory_IsSkippedEvenWithTrailingSeparator(bool trailingSeparator)
    {
        _root.CreateFile("src/a.txt");
        _root.CreateFile("src/skip/b.txt");
        string excluded = Dir("src", "skip") + (trailingSeparator ? Path.DirectorySeparatorChar.ToString() : "");

        Assert.Equal(["a.txt"], ScanNames(Dir("src"), [excluded]));
    }

    [Fact]
    public void ExcludedSourceItself_ReturnsNoFiles()
    {
        _root.CreateFile("src/a.txt");

        Assert.Empty(ScanNames(Dir("src"), [Dir("src")]));
    }

    [Fact]
    public void UnreadableSubdirectory_IsLoggedAndSkipped()
    {
        if (OperatingSystem.IsWindows()) return;
        _root.CreateFile("src/a.txt");
        _root.CreateFile("src/locked/b.txt");
        string locked = Dir("src", "locked");
        System.IO.File.SetUnixFileMode(locked, UnixFileMode.None);
        try
        {
            // root ignores permissions, then there is nothing to test
            try { Directory.GetFiles(locked); return; } catch (UnauthorizedAccessException) { }

            Assert.Equal(["a.txt"], ScanNames(Dir("src")));
            Assert.Contains(_logger.Entries, e => e.Level == LogLevel.Error);
        }
        finally
        {
            System.IO.File.SetUnixFileMode(locked, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }
    }

    [Fact]
    public void Folders_AreListedDepthFirstAndOrderedByPath()
    {
        Directory.CreateDirectory(Dir("dst", "B"));
        Directory.CreateDirectory(Dir("dst", "A", "Sub"));
        _root.CreateFile("dst/A/file.txt");

        var folders = _scanner.ScanFolders(Dir("dst"), []);

        Assert.Equal(["A", "Sub", "B"], folders.Select(f => f.Name));
        Assert.Equal(Dir("dst", "A", "Sub"), folders[1].Path);
    }

    [Fact]
    public void ExcludedFolder_IsSkippedWithItsSubfolders()
    {
        Directory.CreateDirectory(Dir("dst", "A"));
        Directory.CreateDirectory(Dir("dst", "Skip", "Inner"));

        var folders = _scanner.ScanFolders(Dir("dst"), [Dir("dst", "Skip") + Path.DirectorySeparatorChar]);

        Assert.Equal(["A"], folders.Select(f => f.Name));
    }

    [Fact]
    public void MissingDestination_ReturnsNoFolders()
    {
        Assert.Empty(_scanner.ScanFolders(Dir("missing"), []));
    }
}
