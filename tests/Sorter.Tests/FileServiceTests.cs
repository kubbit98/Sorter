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

    private FileService CreateService(Action<ConfigOptions>? configure = null, bool useThumbnails = false)
    {
        var options = new ConfigOptions { Source = _source, Destination = _destination };
        configure?.Invoke(options);
        var optionsMonitor = new TestOptionsMonitor<ConfigOptions>(options);
        var keyBindsMonitor = new TestOptionsMonitor<KeyBindsOptions>(new KeyBindsOptions());
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["UseThumbnails"] = useThumbnails.ToString(), ["Password"] = "" })
            .Build();
        var configOptionsService = new ConfigOptionsService(
            new DestinationDFP(_destination, NullLogger.Instance),
            new SourceDFP(_source, NullLogger.Instance),
            optionsMonitor,
            keyBindsMonitor,
            configuration);
        return new FileService(configuration, optionsMonitor, keyBindsMonitor, configOptionsService,
            _logger, new ThumbnailScheduler(_thumbnailGenerator, NullLogger<ThumbnailScheduler>.Instance), new FileScanner(NullLogger<FileScanner>.Instance), new FileOperations(NullLogger<FileOperations>.Instance));
    }

    private async Task<FileService> CreateInitializedServiceAsync(Action<ConfigOptions>? configure = null, bool useThumbnails = false)
    {
        var service = CreateService(configure, useThumbnails);
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
        CreateSourceFile("long.txt", new string('x', FileOperations.MaxTextPreviewLength + 1000));
        var service = await CreateInitializedServiceAsync();

        string content = service.GetTextFileContent(service.GetFileAtIndex(0)!);

        Assert.Equal(new string('x', FileOperations.MaxTextPreviewLength) + "[...]", content);
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
    public async Task InitializeFiles_WaitsForFirstThumbnailAndSchedulesTheNextFiles()
    {
        for (int i = 0; i < 8; i++) CreateSourceFile($"{i}.jpg");
        _thumbnailGenerator.Result = new ThumbnailResult(Path.Combine(_root.Path, "thumb.jpg"), null);

        var service = await CreateInitializedServiceAsync(useThumbnails: true);

        Assert.Equal("/tmp/thumb.jpg", service.GetFileAtIndex(0)!.ThumbnailPath);
        await WaitUntil(() => _thumbnailGenerator.Calls == ThumbnailScheduler.FilesAhead);
        Assert.Equal(ThumbnailScheduler.FilesAhead, _thumbnailGenerator.Calls);
    }

    [Fact]
    public async Task GetNextIndex_SchedulesFilesAheadOfTheUser()
    {
        for (int i = 0; i < 8; i++) CreateSourceFile($"{i}.jpg");
        var service = await CreateInitializedServiceAsync(useThumbnails: true);

        for (int i = 0; i < 4; i++) service.GetNextIndex();

        // the 4th file is index 3, files 3..7 are scheduled, all 8 in total
        await WaitUntil(() => _thumbnailGenerator.Calls == 8);
        Assert.Equal(8, _thumbnailGenerator.Calls);
    }

    [Fact]
    public async Task WithoutThumbnails_GeneratorIsNeverCalled()
    {
        CreateSourceFile("a.jpg");
        var service = await CreateInitializedServiceAsync();

        service.GetNextIndex();

        await Task.Delay(100);
        Assert.Equal(0, _thumbnailGenerator.Calls);
    }

    private static async Task WaitUntil(Func<bool> condition)
    {
        for (int i = 0; i < 100 && !condition(); i++) await Task.Delay(20);
    }

    [Fact]
    public async Task GetNextIndex_FromManySessionsAtOnce_HandsOutEachFileOnce()
    {
        const int fileCount = 300;
        for (int i = 0; i < fileCount; i++) CreateSourceFile($"{i:D3}.txt");
        var service = await CreateInitializedServiceAsync();

        for (int round = 0; round < 20; round++)
        {
            Assert.True(await service.InitializeFiles(true));
            var indexes = new System.Collections.Concurrent.ConcurrentBag<int?>();
            using var start = new Barrier(8);

            // 8 sessions asking at the same moment, together more often than there are files
            await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => Task.Run(() =>
            {
                start.SignalAndWait();
                for (int i = 0; i < fileCount / 4; i++) indexes.Add(service.GetNextIndex());
            })));

            var handedOut = indexes.Where(i => i != null).Select(i => i!.Value).ToList();
            Assert.Equal(Enumerable.Range(0, fileCount), handedOut.Order());
        }
    }

    [Fact]
    public async Task CreateFolder_FromManySessionsAtOnce_CreatesTheFolderOnce()
    {
        var service = await CreateInitializedServiceAsync();

        for (int round = 0; round < 50; round++)
        {
            string name = $"New{round}";
            using var start = new Barrier(8);
            var results = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => Task.Run(() =>
            {
                start.SignalAndWait();
                return service.CreateFolder(name);
            })));

            Assert.Single(results, created => created);
        }
        Assert.Equal(50, (await service.GetFoldersAsync()).Length);
    }

    [Fact]
    public async Task GetFileAtIndex_DuringRenames_NeverSeesHalfUpdatedFile()
    {
        CreateSourceFile("a.txt");
        var service = await CreateInitializedServiceAsync();
        using var cancellation = new CancellationTokenSource();

        var reader = Task.Run(() =>
        {
            int checks = 0;
            while (!cancellation.IsCancellationRequested)
            {
                var file = service.GetFileAtIndex(0)!;
                Assert.Equal(file.NameWithExtension, Path.GetFileName(file.PhysicalPath));
                checks++;
            }
            return checks;
        });
        for (int i = 0; i < 200; i++) service.ChangeFileName(0, i % 2 == 0 ? "b" : "a");
        cancellation.Cancel();

        Assert.True(await reader > 0);
    }
}
