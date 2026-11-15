using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ImageMagick;

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

/// <summary>Basic properties of an image file, read independently of the generator under test.</summary>
internal sealed record ImageFileInfo(int Width, int Height, string Format, int Frames, ushort? Orientation);

internal static class TestImages
{
    // the app's ImageMagick security policy must be in place before any test uses ImageMagick
    static TestImages() => Sorter.Data.MagickThumbnailGenerator.EnsureInitialized();

    public static string TestDataPath(string fileName) => Path.Combine(AppContext.BaseDirectory, "TestData", fileName);

    /// <param name="format">Saves in this format regardless of the file extension, e.g. "jpg" or "png".</param>
    public static string Create(string path, int width, int height, ushort? exifOrientation = null, string? format = null)
    {
        using var image = Solid(width, height, 40, 90, 160);
        if (exifOrientation != null)
        {
            var exif = new ExifProfile();
            exif.SetValue(ExifTag.Orientation, exifOrientation.Value);
            image.SetProfile(exif);
            // ImageMagick writes this property into the EXIF tag when saving, it would overwrite it with "undefined"
            image.Orientation = (Orientation)exifOrientation.Value;
        }
        image.Write(path, ToMagickFormat(format ?? Path.GetExtension(path).TrimStart('.')));
        return path;
    }

    public static string CreateAnimatedGif(string path, int width, int height, int frames)
    {
        using var images = new MagickImageCollection();
        for (int i = 0; i < frames; i++)
        {
            var frame = Solid(width, height, (byte)(i * 60), 0, 0);
            frame.AnimationDelay = 10;
            images.Add(frame);
        }
        images.Write(path, MagickFormat.Gif);
        return path;
    }

    /// <summary>32-bit BMP with alpha, one of the variants browsers don't display reliably.</summary>
    public static string CreateBmpWithTransparentCorner(string path, int width, int height)
    {
        using var image = Solid(width, height, 255, 0, 0, alpha: 255);
        using (var pixels = image.GetPixels())
        {
            pixels.SetPixel(0, 0, [0, 0, 0, 0]);
        }
        image.Write(path, MagickFormat.Bmp);
        return path;
    }

    public static ImageFileInfo Identify(string path)
    {
        using var images = new MagickImageCollection(path);
        var first = images[0];
        ushort? orientation = first.GetExifProfile()?.GetValue(ExifTag.Orientation)?.Value;
        return new ImageFileInfo((int)first.Width, (int)first.Height, first.Format.ToString().ToUpperInvariant(), images.Count, orientation);
    }

    public static int AlphaAt(string path, int x, int y)
    {
        using var image = new MagickImage(path);
        using var pixels = image.GetPixels();
        return pixels.GetPixel(x, y).ToColor()!.A;
    }

    // built from raw pixels: ImageMagick's usual "xc:" color source is a format the app's security policy blocks
    private static MagickImage Solid(int width, int height, byte red, byte green, byte blue, byte? alpha = null)
    {
        byte[] color = alpha == null ? [red, green, blue] : [red, green, blue, alpha.Value];
        byte[] pixels = new byte[width * height * color.Length];
        for (int i = 0; i < pixels.Length; i += color.Length) color.CopyTo(pixels, i);
        var mapping = alpha == null ? PixelMapping.RGB : PixelMapping.RGBA;
        return new MagickImage(pixels, new PixelReadSettings((uint)width, (uint)height, StorageType.Char, mapping));
    }

    private static MagickFormat ToMagickFormat(string extension) => extension.ToLowerInvariant() switch
    {
        "jpg" or "jpeg" => MagickFormat.Jpeg,
        "png" => MagickFormat.Png,
        "gif" => MagickFormat.Gif,
        "bmp" => MagickFormat.Bmp,
        var other => throw new ArgumentException($"No test format for '{other}'"),
    };
}
