using System.Collections.Concurrent;

namespace Sorter.Data;

/// <summary>
/// Creates thumbnails in the background a few files ahead of the user, each file at most once,
/// even when several sessions ask for it at the same time.
/// </summary>
public class ThumbnailScheduler(IThumbnailGenerator generator, ILogger<ThumbnailScheduler> logger)
{
    public const int FilesAhead = 5;

    // Lazy makes sure only one task is started per file, GetOrAdd alone may run the factory twice
    private readonly ConcurrentDictionary<File, Lazy<Task>> _tasks = new(ReferenceEqualityComparer.Instance);

    /// <summary>
    /// Starts creating the thumbnail in the background. The returned task never fails,
    /// errors end up in the log and in the file state.
    /// </summary>
    public Task Schedule(File file) =>
        _tasks.GetOrAdd(file, f => new Lazy<Task>(() => Task.Run(() => CreateThumbnail(f)))).Value;

    public void ScheduleAhead(IReadOnlyList<File> files, int startIndex)
    {
        for (int i = Math.Max(startIndex, 0); i < Math.Min(files.Count, startIndex + FilesAhead); i++)
        {
            Schedule(files[i]);
        }
    }

    /// <summary>Forgets files of the previous session, they are never scheduled again.</summary>
    public void Reset() => _tasks.Clear();

    public void CreateThumbnail(File file)
    {
        if (file.IsThumbnailExist != File.ThumbnailEnum.Unknown) return;
        if (!generator.CanProcess(file.Extension))
        {
            file.IsThumbnailExist = File.ThumbnailEnum.WillNot;
            return;
        }
        file.IsThumbnailExist = File.ThumbnailEnum.WillBe;
        try
        {
            ThumbnailResult result = generator.Generate(file.PhysicalPath, file.Extension);
            if (result.ConvertedPath != null) file.ConvertedPath = MediaUrlBuilder.GetTempUrl(result.ConvertedPath);
            if (result.ThumbnailPath != null) file.ThumbnailPath = MediaUrlBuilder.GetTempUrl(result.ThumbnailPath);
            file.IsThumbnailExist = result.ThumbnailPath != null ? File.ThumbnailEnum.Exists : File.ThumbnailEnum.WillNot;
        }
        catch (InvalidImageFileException e)
        {
            logger.LogWarning("Thumbnail cannot be created for {FilePath}: {Reason}", file.PhysicalPath, e.Message);
            file.IsThumbnailExist = File.ThumbnailEnum.WillNot;
        }
        catch (Exception e)
        {
            logger.LogError(e, "Thumbnail cannot be created for {FilePath}", file.PhysicalPath);
            file.IsThumbnailExist = File.ThumbnailEnum.WillNot;
        }
    }
}
