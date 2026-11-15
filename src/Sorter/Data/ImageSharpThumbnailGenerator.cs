using HeyRed.ImageSharp.Heif.Formats.Avif;
using HeyRed.ImageSharp.Heif.Formats.Heif;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Processing;

namespace Sorter.Data;

public class ImageSharpThumbnailGenerator : IThumbnailGenerator
{
    public const int DefaultMaxSize = 850;

    private static readonly string[] s_supportedExtensions = ["bmp", "gif", "jpg", "jpeg", "pbm", "png", "tiff", "tga", "webp", "heic"];
    // browsers (except Safari) can't render these in <img>, so a JPG conversion is shown instead
    private static readonly string[] s_browserUnsupportedExtensions = ["heic"];
    private static readonly DecoderOptions s_heifDecoderOptions = new()
    {
        Configuration = new Configuration(new AvifConfigurationModule(), new HeifConfigurationModule())
    };

    private readonly string _outputDirectory;
    private readonly int _maxSize;

    public ImageSharpThumbnailGenerator(string outputDirectory, int maxSize = DefaultMaxSize)
    {
        _outputDirectory = Path.GetFullPath(outputDirectory);
        _maxSize = maxSize;
        Directory.CreateDirectory(_outputDirectory);
    }

    public bool CanProcess(string extension) => s_supportedExtensions.Contains(extension.ToLowerInvariant());

    public ThumbnailResult Generate(string physicalPath, string extension)
    {
        string normalizedExtension = extension.ToLowerInvariant();
        bool convert = s_browserUnsupportedExtensions.Contains(normalizedExtension);
        using Image image = Load(physicalPath, convert);
        // the HEIF decoder configuration has no JPG encoder, so it must be passed explicitly
        IImageEncoder? encoder = convert ? new JpegEncoder() : null;
        string outputExtension = convert ? "jpg" : normalizedExtension;

        string? convertedPath = convert ? Save(image, outputExtension, encoder) : null;
        double ratio = Math.Min(_maxSize / (double)image.Width, _maxSize / (double)image.Height);
        if (ratio >= 1) return new ThumbnailResult(null, convertedPath);

        image.Mutate(x => x.Resize((int)(image.Width * ratio), (int)(image.Height * ratio)));
        return new ThumbnailResult(Save(image, outputExtension, encoder), convertedPath);
    }

    private static Image Load(string physicalPath, bool heif)
    {
        try
        {
            return heif ? Image.Load(s_heifDecoderOptions, physicalPath) : Image.Load(physicalPath);
        }
        catch (UnknownImageFormatException e)
        {
            throw new InvalidImageFileException("unrecognized image format", e);
        }
        catch (InvalidImageContentException e)
        {
            throw new InvalidImageFileException(e.Message, e);
        }
        // the libheif error code isn't public, its category is only the message prefix; other categories
        // (e.g. "Unsupported feature" for a missing HEVC decoder) are setup problems, not broken files
        catch (LibHeifSharp.HeifException e) when (e.Message.StartsWith("Invalid input", StringComparison.Ordinal))
        {
            throw new InvalidImageFileException(e.Message, e);
        }
    }

    private string Save(Image image, string extension, IImageEncoder? encoder)
    {
        // a new random file every time, so a path is never handed out before its file is fully written
        string path;
        do
        {
            path = Path.Combine(_outputDirectory, Path.ChangeExtension(Path.GetRandomFileName(), extension));
        } while (System.IO.File.Exists(path));
        if (encoder == null) image.Save(path);
        else image.Save(path, encoder);
        return path;
    }
}
