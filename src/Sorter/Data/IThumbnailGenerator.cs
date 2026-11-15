namespace Sorter.Data;

public interface IThumbnailGenerator
{
    bool CanProcess(string extension);

    /// <summary>
    /// Creates files for displaying the image in the browser and returns their physical paths.
    /// ThumbnailPath is null when the image is small enough to be shown as is,
    /// ConvertedPath is set only for formats that browsers can't display.
    /// </summary>
    ThumbnailResult Generate(string physicalPath, string extension);
}

public record ThumbnailResult(string? ThumbnailPath, string? ConvertedPath);
