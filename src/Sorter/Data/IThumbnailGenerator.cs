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

/// <summary>
/// The file content is damaged or isn't an image at all. Expected for user files,
/// unlike other exceptions thrown by a generator.
/// </summary>
public class InvalidImageFileException(string message, Exception innerException) : Exception(message, innerException);
