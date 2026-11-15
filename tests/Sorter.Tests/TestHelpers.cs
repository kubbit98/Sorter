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

    public static string Create(string path, int width, int height, ushort? exifOrientation = null)
    {
        using var image = new Image<Rgb24>(width, height, new Rgb24(40, 90, 160));
        if (exifOrientation != null)
        {
            image.Metadata.ExifProfile = new ExifProfile();
            image.Metadata.ExifProfile.SetValue(ExifTag.Orientation, exifOrientation.Value);
        }
        image.Save(path);
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
