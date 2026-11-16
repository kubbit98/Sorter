using Microsoft.Extensions.Logging;
using Sorter.Data;
using SorterFile = Sorter.Data.File;

namespace Sorter.Tests;

public class ThumbnailSchedulerTests
{
    private readonly FakeThumbnailGenerator _generator = new();
    private readonly ListLogger<ThumbnailScheduler> _logger = new();
    private readonly ThumbnailScheduler _scheduler;

    public ThumbnailSchedulerTests()
    {
        _scheduler = new ThumbnailScheduler(_generator, _logger);
    }

    private static SorterFile Jpg(string name = "a") => new($"/x/{name}.jpg", name, "jpg");

    [Fact]
    public void CreateThumbnail_SkipsUnsupportedFiles()
    {
        var file = new SorterFile("/x/a.mp4", "a", "mp4");

        _scheduler.CreateThumbnail(file);

        Assert.Equal(SorterFile.ThumbnailEnum.WillNot, file.IsThumbnailExist);
        Assert.Equal(0, _generator.Calls);
    }

    [Fact]
    public void CreateThumbnail_SetsTempUrlWhenThumbnailIsCreated()
    {
        _generator.Result = new ThumbnailResult("/tmp-dir/abc.jpg", null);
        var file = new SorterFile("/x/a.JPG", "a", "JPG");

        _scheduler.CreateThumbnail(file);

        Assert.Equal(SorterFile.ThumbnailEnum.Exists, file.IsThumbnailExist);
        Assert.Equal("/tmp/abc.jpg", file.ThumbnailPath);
        Assert.Null(file.ConvertedPath);
    }

    [Fact]
    public void CreateThumbnail_SetsConversionWithoutThumbnail()
    {
        _generator.Result = new ThumbnailResult(null, "/tmp-dir/conv.jpg");
        var file = new SorterFile("/x/a.heic", "a", "heic");

        _scheduler.CreateThumbnail(file);

        Assert.Equal(SorterFile.ThumbnailEnum.WillNot, file.IsThumbnailExist);
        Assert.Equal("/tmp/conv.jpg", file.ConvertedPath);
        Assert.Null(file.ThumbnailPath);
    }

    [Fact]
    public void CreateThumbnail_LogsBrokenImageAsShortWarning()
    {
        _generator.Exception = new InvalidImageFileException("unrecognized image format", new Exception("details"));
        var file = Jpg();

        _scheduler.CreateThumbnail(file);

        Assert.Equal(SorterFile.ThumbnailEnum.WillNot, file.IsThumbnailExist);
        var entry = Assert.Single(_logger.Entries);
        Assert.Equal(LogLevel.Warning, entry.Level);
        Assert.Equal("Thumbnail cannot be created for /x/a.jpg: unrecognized image format", entry.Message);
        Assert.Null(entry.Exception);
    }

    [Fact]
    public void CreateThumbnail_LogsUnexpectedErrorWithException()
    {
        _generator.Exception = new InvalidOperationException("unexpected");
        var file = Jpg();

        _scheduler.CreateThumbnail(file);

        Assert.Equal(SorterFile.ThumbnailEnum.WillNot, file.IsThumbnailExist);
        var entry = Assert.Single(_logger.Entries);
        Assert.Equal(LogLevel.Error, entry.Level);
        Assert.Same(_generator.Exception, entry.Exception);
    }

    [Fact]
    public void CreateThumbnail_ProcessesEachFileOnlyOnce()
    {
        var file = Jpg();

        _scheduler.CreateThumbnail(file);
        _scheduler.CreateThumbnail(file);

        Assert.Equal(1, _generator.Calls);
    }

    [Fact]
    public async Task Schedule_CompletesWhenThumbnailIsDone()
    {
        _generator.Result = new ThumbnailResult("/tmp-dir/abc.jpg", null);
        var file = Jpg();

        await _scheduler.Schedule(file);

        Assert.Equal(SorterFile.ThumbnailEnum.Exists, file.IsThumbnailExist);
    }

    [Fact]
    public async Task Schedule_NeverFailsEvenWhenGeneratorThrows()
    {
        _generator.Exception = new InvalidOperationException("unexpected");
        var file = Jpg();

        await _scheduler.Schedule(file);

        Assert.Equal(SorterFile.ThumbnailEnum.WillNot, file.IsThumbnailExist);
    }

    [Fact]
    public async Task Schedule_FromManyThreadsAtOnce_GeneratesOnce()
    {
        using var gate = new ManualResetEventSlim(false);
        _generator.Gate = gate;
        var file = Jpg();

        // all calls arrive while the first generation is still running
        Task[] tasks = await Task.WhenAll(Enumerable.Range(0, 20).Select(_ => Task.Factory.StartNew(() => _scheduler.Schedule(file))));
        gate.Set();
        await Task.WhenAll(tasks);

        Assert.Equal(1, _generator.Calls);
        Assert.All(tasks, task => Assert.Same(tasks[0], task));
    }

    [Theory]
    [InlineData(0, 10, new[] { 0, 1, 2, 3, 4 })]
    [InlineData(2, 10, new[] { 2, 3, 4, 5, 6 })]
    [InlineData(7, 10, new[] { 7, 8, 9 })]
    [InlineData(3, 3, new int[0])]
    public void ScheduleAhead_StartsFilesAheadFromStartIndexWithinList(int startIndex, int count, int[] expected)
    {
        var files = Enumerable.Range(0, count).Select(i => Jpg(i.ToString())).ToList();

        _scheduler.ScheduleAhead(files, startIndex);

        Assert.Equal(expected, Enumerable.Range(0, count).Where(i => _scheduler.IsScheduled(files[i])));
    }
}
