using System.Text;
using Microsoft.Extensions.Logging;
using Sorter.Data;

namespace Sorter.Tests;

public class FileOperationsTests : IDisposable
{
    private readonly TempDirectory _root = new();
    private readonly ListLogger<FileOperations> _logger = new();
    private readonly FileOperations _operations;

    public FileOperationsTests()
    {
        _operations = new FileOperations(_logger);
    }

    public void Dispose() => _root.Dispose();

    [Theory]
    [InlineData("photo", "JPG", "photo.JPG")]
    [InlineData("README", "", "README")]
    [InlineData("", "txt", ".txt")]
    public void CombineFileName_AddsDotOnlyWithExtension(string name, string extension, string expected)
    {
        Assert.Equal(expected, FileOperations.CombineFileName(name, extension));
    }

    [Theory]
    [InlineData("holiday", true)]
    [InlineData("zdjęcie z wakacji #1", true)]
    [InlineData(".hidden", true)]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("   ", false)]
    [InlineData(".", false)]
    [InlineData("..", false)]
    [InlineData("a/b", false)]
    [InlineData("a\0b", false)]
    public void IsValidFileName_RejectsNamesThatDontNameAFile(string? name, bool expected)
    {
        Assert.Equal(expected, FileOperations.IsValidFileName(name));
    }

    [Fact]
    public void IsNameFree_ChecksTargetDirectory()
    {
        _root.CreateFile("A/taken.jpg");

        Assert.False(_operations.IsNameFree(_root.Combine("A"), "taken.jpg"));
        Assert.True(_operations.IsNameFree(_root.Combine("A"), "free.jpg"));
    }

    [Fact]
    public void Move_MovesFileAndReturnsNewPath()
    {
        string file = _root.CreateFile("src/a.jpg");
        string target = Directory.CreateDirectory(_root.Combine("A")).FullName;

        string newPath = _operations.Move(file, target);

        Assert.Equal(Path.Combine(target, "a.jpg"), newPath);
        Assert.True(System.IO.File.Exists(newPath));
        Assert.False(System.IO.File.Exists(file));
        Assert.Contains(_logger.Entries, e => e.Level == LogLevel.Information);
    }

    [Fact]
    public void Move_DoesNotOverwriteAndLogsError()
    {
        string file = _root.CreateFile("src/a.jpg", "new");
        _root.CreateFile("A/a.jpg", "existing");

        Assert.ThrowsAny<IOException>(() => _operations.Move(file, _root.Combine("A")));

        Assert.Equal("existing", System.IO.File.ReadAllText(_root.Combine("A", "a.jpg")));
        Assert.True(System.IO.File.Exists(file));
        Assert.Contains(_logger.Entries, e => e.Level == LogLevel.Error);
    }

    [Theory]
    [InlineData("JPG", "holiday.JPG")]
    [InlineData("", "holiday")]
    public void Rename_KeepsExtensionAndAddsNoTrailingDot(string extension, string expectedName)
    {
        string file = _root.CreateFile(FileOperations.CombineFileName("old", extension));

        string? newPath = _operations.Rename(file, "holiday", extension);

        Assert.Equal(_root.Combine(expectedName), newPath);
        Assert.True(System.IO.File.Exists(newPath));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(".")]
    [InlineData("..")]
    [InlineData("a/b")]
    [InlineData("a\0b")]
    public void Rename_RejectsInvalidNamesWithoutTouchingTheFile(string newName)
    {
        string file = _root.CreateFile("old.jpg");

        Assert.Null(_operations.Rename(file, newName, "jpg"));
        Assert.True(System.IO.File.Exists(file));
    }

    [Fact]
    public void Rename_ThrowsWhenAnotherFileHasTheName()
    {
        string file = _root.CreateFile("a.jpg", "a");
        _root.CreateFile("b.jpg", "b");

        Assert.Throws<InvalidOperationException>(() => _operations.Rename(file, "b", "jpg"));
        Assert.Equal("b", System.IO.File.ReadAllText(_root.Combine("b.jpg")));
    }

    [Fact]
    public void Rename_ToTheSameName_IsNoOp()
    {
        string file = _root.CreateFile("a.jpg");

        Assert.Equal(file, _operations.Rename(file, "a", "jpg"));
        Assert.True(System.IO.File.Exists(file));
    }

    [Fact]
    public void CreateFolder_CreatesFolderInParent()
    {
        string? path = _operations.CreateFolder(_root.Path, "New");

        Assert.Equal(_root.Combine("New"), path);
        Assert.True(Directory.Exists(path));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(".")]
    [InlineData("..")]
    [InlineData("a/b")]
    public void CreateFolder_RejectsNamesThatDontCreateAChildFolder(string name)
    {
        string parent = Directory.CreateDirectory(_root.Combine("dst")).FullName;

        Assert.Null(_operations.CreateFolder(parent, name));
        Assert.Empty(Directory.GetDirectories(parent));
    }

    [Theory]
    [InlineData(10, false)]
    [InlineData(FileOperations.MaxTextPreviewLength, false)]
    [InlineData(FileOperations.MaxTextPreviewLength + 1, true)]
    [InlineData(1_000_000, true)]
    public void ReadTextPreview_TruncatesLongContent(int length, bool truncated)
    {
        string file = _root.CreateFile("text.txt", new string('x', length));

        string preview = _operations.ReadTextPreview(file);

        string expected = truncated ? new string('x', FileOperations.MaxTextPreviewLength) + "[...]" : new string('x', length);
        Assert.Equal(expected, preview);
    }

    [Fact]
    public void ReadTextPreview_DecodesUtf8WithAndWithoutBom()
    {
        string withBom = _root.Combine("bom.txt");
        string withoutBom = _root.Combine("plain.txt");
        System.IO.File.WriteAllText(withBom, "Zażółć gęślą jaźń", new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
        System.IO.File.WriteAllText(withoutBom, "Zażółć gęślą jaźń", new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

        Assert.Equal("Zażółć gęślą jaźń", _operations.ReadTextPreview(withBom));
        Assert.Equal("Zażółć gęślą jaźń", _operations.ReadTextPreview(withoutBom));
    }
}
