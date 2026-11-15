using HeyRed.ImageSharp.Heif.Formats.Avif;
using HeyRed.ImageSharp.Heif.Formats.Heif;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Processing;

namespace Sorter.Data;

public class ImageSharpThumbnailGenerator : IThumbnailGenerator
{
    public const int DefaultMaxSize = 850;

    private static readonly string[] s_supportedExtensions = ["bmp", "gif", "jpg", "jpeg", "jfif", "jpe", "pbm", "png", "tiff", "tga", "webp", "heic", "heif", "hif"];
    // .hif is what Canon, Fujifilm and Sony cameras use for HEIF photos
    private static readonly string[] s_heifExtensions = ["heic", "heif", "hif"];
    // formats browsers can't display reliably are always converted, the encoder is passed explicitly
    // because the HEIF decoder configuration doesn't contain any encoders
    private static readonly Dictionary<string, (string Extension, Func<IImageEncoder> CreateEncoder)> s_conversions = new()
    {
        // only Safari renders HEIF
        ["heic"] = ("jpg", () => new JpegEncoder()),
        ["heif"] = ("jpg", () => new JpegEncoder()),
        ["hif"] = ("jpg", () => new JpegEncoder()),
        // browsers render only common BMP variants (no RLE compression, alpha or unusual bit depths),
        // PNG is lossless and keeps transparency
        ["bmp"] = ("png", () => new PngEncoder()),
    };
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
        bool convert = s_conversions.TryGetValue(normalizedExtension, out var conversion);
        using Image image = Load(physicalPath, heif: s_heifExtensions.Contains(normalizedExtension));
        IImageEncoder? encoder = convert ? conversion.CreateEncoder() : null;
        // the decoded format decides, so .jfif/.jpe thumbnails get .jpg and a PNG named .jpg gets .png
        string outputExtension = convert
            ? conversion.Extension
            : image.Metadata.DecodedImageFormat?.FileExtensions.FirstOrDefault() ?? normalizedExtension;

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
