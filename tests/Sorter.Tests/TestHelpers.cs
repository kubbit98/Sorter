using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Metadata.Profiles.Exif;
using SixLabors.ImageSharp.PixelFormats;

namespace Sorter.Tests;

internal sealed class TestOptionsMonitor<T>(T value) : IOptionsMonitor<T>
{
    public T CurrentValue { get; set; } = value;
    public T Get(string? name) => CurrentValue;
    public IDisposable? OnChange(Action<T, string?> listener) => null;
}

internal sealed class ListLogger<T> : ILogger<T>
{
    private readonly List<(LogLevel Level, string Message, Exception? Exception)> _entries = [];

    public IReadOnlyList<(LogLevel Level, string Message, Exception? Exception)> Entries
    {
        get { lock (_entries) return [.. _entries]; }
    }

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        lock (_entries) _entries.Add((logLevel, formatter(state, exception), exception));
    }
}

internal sealed class FakeThumbnailGenerator : Sorter.Data.IThumbnailGenerator
{
    private int _calls;

    public Sorter.Data.ThumbnailResult Result { get; set; } = new(null, null);
    public Exception? Exception { get; set; }
    /// <summary>When set, Generate waits for it, to keep a generation in progress.</summary>
    public ManualResetEventSlim? Gate { get; set; }
    public int Calls => Volatile.Read(ref _calls);

    public bool CanProcess(string extension) => extension.ToLowerInvariant() is "jpg" or "heic";

    public Sorter.Data.ThumbnailResult Generate(string physicalPath, string extension)
    {
        Interlocked.Increment(ref _calls);
        Gate?.Wait(TimeSpan.FromSeconds(10));
        if (Exception != null) throw Exception;
        return Result;
    }
}

internal sealed class TempDirectory : IDisposable
{
    public string Path { get; } = Directory.CreateTempSubdirectory("sorter-tests-").FullName;

    public string Combine(params string[] parts) => System.IO.Path.Combine([Path, .. parts]);

    public string CreateFile(string relativePath, string content = "content")
    {
        string fullPath = Combine(relativePath);
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(fullPath)!);
        System.IO.File.WriteAllText(fullPath, content);
        return fullPath;
    }

    public void Dispose() => Directory.Delete(Path, recursive: true);
}

internal static class TestImages
{
    public static string TestDataPath(string fileName) => Path.Combine(AppContext.BaseDirectory, "TestData", fileName);

    /// <param name="format">Saves in this format regardless of the file extension, e.g. "jpg" or "png".</param>
    public static string Create(string path, int width, int height, ushort? exifOrientation = null, string? format = null)
    {
        using var image = new Image<Rgb24>(width, height, new Rgb24(40, 90, 160));
        if (exifOrientation != null)
        {
            image.Metadata.ExifProfile = new ExifProfile();
            image.Metadata.ExifProfile.SetValue(ExifTag.Orientation, exifOrientation.Value);
        }
        if (format == null)
        {
            image.Save(path);
        }
        else
        {
            Configuration.Default.ImageFormatsManager.TryFindFormatByFileExtension(format, out var imageFormat);
            image.Save(path, Configuration.Default.ImageFormatsManager.GetEncoder(imageFormat!));
        }
        return path;
    }

    public static string CreateAnimatedGif(string path, int width, int height, int frames)
    {
        using var image = new Image<Rgb24>(width, height, new Rgb24(0, 0, 0));
        for (int i = 1; i < frames; i++)
        {
            using var frame = new Image<Rgb24>(width, height, new Rgb24((byte)(i * 60), 0, 0));
            image.Frames.AddFrame(frame.Frames.RootFrame);
        }
        image.Save(path);
        return path;
    }
}
