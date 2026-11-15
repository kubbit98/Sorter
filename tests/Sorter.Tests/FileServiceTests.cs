using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Sorter.Data;
using SorterFile = Sorter.Data.File;

namespace Sorter.Tests;

public class FileServiceTests : IDisposable
{
    private readonly TempDirectory _root = new();
    private readonly string _source;
    private readonly string _destination;
    private readonly FakeThumbnailGenerator _thumbnailGenerator = new();
    private readonly ListLogger<FileService> _logger = new();

    public FileServiceTests()
    {
        _source = Directory.CreateDirectory(_root.Combine("src")).FullName;
        _destination = Directory.CreateDirectory(_root.Combine("dst")).FullName;
    }

    public void Dispose() => _root.Dispose();

    private FileService CreateService(Action<ConfigOptions>? configure = null)
    {
        var options = new ConfigOptions { Source = _source, Destination = _destination };
        configure?.Invoke(options);
        var optionsMonitor = new TestOptionsMonitor<ConfigOptions>(options);
        var keyBindsMonitor = new TestOptionsMonitor<KeyBindsOptions>(new KeyBindsOptions());
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["UseThumbnails"] = "false", ["Password"] = "" })
            .Build();
        var configOptionsService = new ConfigOptionsService(
            new DestinationDFP(_destination, NullLogger.Instance),
            new SourceDFP(_source, NullLogger.Instance),
            optionsMonitor,
            keyBindsMonitor);
        return new FileService(configuration, optionsMonitor, keyBindsMonitor, configOptionsService,
            _logger, _thumbnailGenerator, new FileScanner(NullLogger<FileScanner>.Instance), new FileOperations(NullLogger<FileOperations>.Instance));
    }

    private async Task<FileService> CreateInitializedServiceAsync(Action<ConfigOptions>? configure = null)
    {
        var service = CreateService(configure);
        Assert.True(await service.InitializeFiles(false));
        return service;
    }

    private static List<string> ListFileNames(FileService service)
    {
        var names = new List<string>();
        for (int i = 0; service.GetFileAtIndex(i) is { } file; i++) names.Add(file.NameWithExtension);
        return names;
    }

    private string CreateSourceFile(string relativePath, string content = "content") =>
        _root.CreateFile(Path.Combine("src", relativePath), content);

    [Fact]
    public async Task InitializeFiles_ReturnsFalseWhenSourceDoesNotExist()
    {
        var service = CreateService(o => o.Source = _root.Combine("missing"));

        Assert.False(await service.InitializeFiles(false));
    }

    [Fact]
    public async Task InitializeFiles_ToleratesBlankAndNullListsInConfig()
    {
        CreateSourceFile("a.txt");

        var service = await CreateInitializedServiceAsync(o =>
        {
            o.ExcludeDirsSource = [""];
            o.WhiteList = null!;
        });

        Assert.Equal(["a.txt"], ListFileNames(service));
    }

    [Fact]
    public async Task Files_AreListedRecursivelyFilesBeforeSubfolders()
    {
        CreateSourceFile("b.txt");
        CreateSourceFile("a.txt");
        CreateSourceFile(Path.Combine("sub", "c.txt"));

        var service = await CreateInitializedServiceAsync();

        Assert.Equal(["a.txt", "b.txt", "c.txt"], ListFileNames(service));
    }

    [Fact]
    public async Task DsStore_IsAlwaysSkipped()
    {
        CreateSourceFile(".DS_Store");
        CreateSourceFile("a.txt");

        var service = await CreateInitializedServiceAsync();

        Assert.Equal(["a.txt"], ListFileNames(service));
    }

    [Fact]
    public async Task Extension_KeepsOriginalCase()
    {
        CreateSourceFile("IMG_0001.JPG");

        var file = (await CreateInitializedServiceAsync()).GetFileAtIndex(0)!;

        Assert.Equal("JPG", file.Extension);
        Assert.Equal("IMG_0001.JPG", file.NameWithExtension);
    }

    [Fact]
    public async Task BlackList_MatchesFileExtensionsIgnoringCase()
    {
        CreateSourceFile("a.mp4");
        CreateSourceFile("b.MP4");
        CreateSourceFile("c.txt");

        var service = await CreateInitializedServiceAsync(o => o.BlackList = ["mp4"]);

        Assert.Equal(["c.txt"], ListFileNames(service));
    }

    [Fact]
    public async Task WhiteList_ListsOnlyGivenExtensions()
    {
        CreateSourceFile("a.mp4");
        CreateSourceFile("b.TXT");

        var service = await CreateInitializedServiceAsync(o =>
        {
            o.UseWhiteListInsteadOfBlackList = true;
            o.WhiteList = ["txt"];
        });

        Assert.Equal(["b.TXT"], ListFileNames(service));
    }

    [Fact]
    public async Task ExcludedSourceDirectory_IsSkipped()
    {
        CreateSourceFile("a.txt");
        CreateSourceFile(Path.Combine("skip", "b.txt"));

        var service = await CreateInitializedServiceAsync(o => o.ExcludeDirsSource = [Path.Combine(_source, "skip")]);

        Assert.Equal(["a.txt"], ListFileNames(service));
    }

    [Theory]
    [InlineData("photo.jpg", "/src/photo.jpg")]
    [InlineData("hash#and%percent.png", "/src/hash%23and%25percent.png")]
    [InlineData("sub folder #1/zdjęcie ą.jpg", "/src/sub%20folder%20%231/zdj%C4%99cie%20%C4%85.jpg")]
    public async Task GetFileAtIndex_ReturnsEscapedUrl(string relativePath, string expectedUrl)
    {
        CreateSourceFile(relativePath.Replace('/', Path.DirectorySeparatorChar));

        var file = (await CreateInitializedServiceAsync()).GetFileAtIndex(0)!;

        Assert.Equal(expectedUrl, file.Path);
    }

    [Fact]
    public async Task GetFileAtIndex_ReturnsNullOutOfRange()
    {
        CreateSourceFile("a.txt");

        var service = await CreateInitializedServiceAsync();

        Assert.Null(service.GetFileAtIndex(1));
        Assert.Null(service.GetFileAtIndex(-1));
    }

    [Fact]
    public async Task GetFileAtIndex_ThrowsWhenNameDoesNotMatch()
    {
        CreateSourceFile("a.txt");

        var service = await CreateInitializedServiceAsync();

        Assert.Throws<Exception>(() => service.GetFileAtIndex(0, "other"));
    }

    [Fact]
    public async Task DestinationFolders_AreListedRecursivelyWithoutExcluded()
    {
        Directory.CreateDirectory(Path.Combine(_destination, "A", "Sub"));
        Directory.CreateDirectory(Path.Combine(_destination, "B"));
        Directory.CreateDirectory(Path.Combine(_destination, "Skip"));

        var service = await CreateInitializedServiceAsync(o => o.ExcludeDirsDestination = [Path.Combine(_destination, "Skip")]);

        var folders = await service.GetFoldersAsync();
        Assert.Equal(["A", "Sub", "B"], folders.Select(f => f.Name));
    }

    [Fact]
    public async Task MoveFile_KeepsExtensionCaseAndUpdatesUrl()
    {
        CreateSourceFile("IMG_0001.JPG");
        string target = Directory.CreateDirectory(Path.Combine(_destination, "A")).FullName;
        var service = await CreateInitializedServiceAsync();

        service.MoveFile(service.GetFileAtIndex(0)!, target);

        Assert.True(System.IO.File.Exists(Path.Combine(target, "IMG_0001.JPG")));
        Assert.Equal("/dest/A/IMG_0001.JPG", service.GetFileAtIndex(0)!.Path);
    }

    [Fact]
    public async Task MovedFile_GetsDestinationUrlWhenDestinationNameStartsWithSourceName()
    {
        // e.g. "Photos" and "Photos-sorted": the moved file's path starts with the source path as plain text
        string destination = Directory.CreateDirectory(_source + "-sorted").FullName;
        string target = Directory.CreateDirectory(Path.Combine(destination, "A")).FullName;
        CreateSourceFile("a.jpg");
        var service = await CreateInitializedServiceAsync(o => o.Destination = destination);

        service.MoveFile(service.GetFileAtIndex(0)!, target);

        Assert.Equal("/dest/A/a.jpg", service.GetFileAtIndex(0)!.Path);
    }

    [Fact]
    public async Task IsDestinationAvailable_IsFalseWhenNameIsTaken()
    {
        CreateSourceFile("a.txt");
        string target = Directory.CreateDirectory(Path.Combine(_destination, "A")).FullName;
        var service = await CreateInitializedServiceAsync();
        var file = service.GetFileAtIndex(0)!;

        Assert.True(await service.IsDestinationAvailable(file, target));
        _root.CreateFile(Path.Combine("dst", "A", "a.txt"));
        Assert.False(await service.IsDestinationAvailable(file, target));
    }

    [Fact]
    public async Task ChangeFileName_KeepsExtensionCase()
    {
        CreateSourceFile("IMG_0001.JPG");
        var service = await CreateInitializedServiceAsync();

        service.ChangeFileName(0, "holiday");

        Assert.True(System.IO.File.Exists(Path.Combine(_source, "holiday.JPG")));
        Assert.Equal("/src/holiday.JPG", service.GetFileAtIndex(0)!.Path);
    }

    [Fact]
    public async Task ChangeFileName_ThrowsWhenTargetExists()
    {
        CreateSourceFile("a.txt");
        CreateSourceFile("b.txt");
        var service = await CreateInitializedServiceAsync();

        Assert.Throws<InvalidOperationException>(() => service.ChangeFileName(0, "b"));
    }

    [Fact]
    public async Task GetTextFileContent_TruncatesLongFiles()
    {
        CreateSourceFile("long.txt", new string('x', 6000));
        var service = await CreateInitializedServiceAsync();

        string content = await service.GetTextFileContent(service.GetFileAtIndex(0)!);

        Assert.Equal(new string('x', 5000) + "[...]", content);
    }

    [Theory]
    [InlineData("..")]
    [InlineData("")]
    public async Task CreateFolder_DoesNotAddFoldersOutsideDestination(string name)
    {
        var service = await CreateInitializedServiceAsync();

        Assert.False(service.CreateFolder(name));
        Assert.Empty(await service.GetFoldersAsync());
    }

    [Fact]
    public async Task ChangeFileName_ToTheSameName_DoesNotThrow()
    {
        CreateSourceFile("a.txt");
        var service = await CreateInitializedServiceAsync();

        service.ChangeFileName(0, "a");

        Assert.Equal("a.txt", service.GetFileAtIndex(0)!.NameWithExtension);
    }

    [Fact]
    public async Task ChangeFileName_OfFileWithoutExtension_AddsNoTrailingDot()
    {
        CreateSourceFile("README");
        var service = await CreateInitializedServiceAsync();

        service.ChangeFileName(0, "NOTES");

        Assert.True(System.IO.File.Exists(Path.Combine(_source, "NOTES")));
        Assert.Equal("/src/NOTES", service.GetFileAtIndex(0)!.Path);
    }

    [Fact]
    public async Task CreateFolder_CreatesFolderAndRejectsDuplicatesAndInvalidNames()
    {
        var service = await CreateInitializedServiceAsync();

        Assert.True(service.CreateFolder("New"));
        Assert.True(Directory.Exists(Path.Combine(_destination, "New")));
        Assert.False(service.CreateFolder("New"));
        Assert.False(service.CreateFolder("a/b"));
    }

    [Fact]
    public void CreateThumbnail_SkipsUnsupportedFiles()
    {
        var file = new SorterFile("/x/a.mp4", "a", "mp4");

        CreateService().CreateThumbnail(file);

        Assert.Equal(SorterFile.ThumbnailEnum.WillNot, file.IsThumbnailExist);
        Assert.Equal(0, _thumbnailGenerator.Calls);
    }

    [Fact]
    public void CreateThumbnail_SetsTempUrlWhenThumbnailIsCreated()
    {
        _thumbnailGenerator.Result = new ThumbnailResult(Path.Combine(_root.Path, "abc.jpg"), null);
        var file = new SorterFile("/x/a.JPG", "a", "JPG");

        CreateService().CreateThumbnail(file);

        Assert.Equal(SorterFile.ThumbnailEnum.Exists, file.IsThumbnailExist);
        Assert.Equal("/tmp/abc.jpg", file.ThumbnailPath);
        Assert.Null(file.ConvertedPath);
    }

    [Fact]
    public void CreateThumbnail_SetsConversionWithoutThumbnail()
    {
        _thumbnailGenerator.Result = new ThumbnailResult(null, Path.Combine(_root.Path, "conv.jpg"));
        var file = new SorterFile("/x/a.heic", "a", "heic");

        CreateService().CreateThumbnail(file);

        Assert.Equal(SorterFile.ThumbnailEnum.WillNot, file.IsThumbnailExist);
        Assert.Equal("/tmp/conv.jpg", file.ConvertedPath);
        Assert.Null(file.ThumbnailPath);
    }

    [Fact]
    public void CreateThumbnail_LogsBrokenImageAsShortWarning()
    {
        _thumbnailGenerator.Exception = new InvalidImageFileException("unrecognized image format", new Exception("details"));
        var file = new SorterFile("/x/a.jpg", "a", "jpg");

        CreateService().CreateThumbnail(file);

        Assert.Equal(SorterFile.ThumbnailEnum.WillNot, file.IsThumbnailExist);
        var entry = Assert.Single(_logger.Entries);
        Assert.Equal(LogLevel.Warning, entry.Level);
        Assert.Equal("Thumbnail cannot be created for /x/a.jpg: unrecognized image format", entry.Message);
        Assert.Null(entry.Exception);
    }

    [Fact]
    public void CreateThumbnail_LogsUnexpectedErrorWithException()
    {
        _thumbnailGenerator.Exception = new InvalidOperationException("unexpected");
        var file = new SorterFile("/x/a.jpg", "a", "jpg");

        CreateService().CreateThumbnail(file);

        Assert.Equal(SorterFile.ThumbnailEnum.WillNot, file.IsThumbnailExist);
        var entry = Assert.Single(_logger.Entries);
        Assert.Equal(LogLevel.Error, entry.Level);
        Assert.Same(_thumbnailGenerator.Exception, entry.Exception);
    }

    [Fact]
    public void CreateThumbnail_ProcessesEachFileOnlyOnce()
    {
        var file = new SorterFile("/x/a.jpg", "a", "jpg");
        var service = CreateService();

        service.CreateThumbnail(file);
        service.CreateThumbnail(file);

        Assert.Equal(1, _thumbnailGenerator.Calls);
    }

    private sealed class FakeThumbnailGenerator : IThumbnailGenerator
    {
        public ThumbnailResult Result { get; set; } = new(null, null);
        public Exception? Exception { get; set; }
        public int Calls { get; private set; }

        public bool CanProcess(string extension) => extension.ToLowerInvariant() is "jpg" or "heic";

        public ThumbnailResult Generate(string physicalPath, string extension)
        {
            Calls++;
            if (Exception != null) throw Exception;
            return Result;
        }
    }
}
