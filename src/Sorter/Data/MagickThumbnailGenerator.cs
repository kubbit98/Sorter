using ImageMagick;
using ImageMagick.Configuration;

namespace Sorter.Data;

public class MagickThumbnailGenerator : IThumbnailGenerator
{
    public const int DefaultMaxSize = 850;

    private static readonly string[] s_rawExtensions = ["dng", "cr2", "cr3", "crw", "nef", "nrw", "arw", "srf", "sr2", "orf", "rw2", "raf", "pef", "srw", "rwl", "3fr", "iiq", "x3f", "kdc", "dcr", "mrw", "erf", "mef", "mos"];
    private static readonly string[] s_supportedExtensions = ["bmp", "gif", "jpg", "jpeg", "jfif", "jpe", "pbm", "png", "tiff", "tif", "tga", "webp", "heic", "heif", "hif", .. s_rawExtensions];
    // formats browsers can't display reliably are always converted
    private static readonly Dictionary<string, (string Extension, MagickFormat Format)> s_conversions = new(
        // camera RAW files are developed by libraw and shown as a JPG
        s_rawExtensions.Select(e => KeyValuePair.Create(e, ("jpg", MagickFormat.Jpeg))))
    {
        // only Safari renders HEIF
        ["heic"] = ("jpg", MagickFormat.Jpeg),
        ["heif"] = ("jpg", MagickFormat.Jpeg),
        // .hif is what Canon, Fujifilm and Sony cameras use for HEIF photos
        ["hif"] = ("jpg", MagickFormat.Jpeg),
        // browsers render only common BMP variants (no RLE compression, alpha or unusual bit depths),
        // PNG is lossless and keeps transparency
        ["bmp"] = ("png", MagickFormat.Png),
        // only Safari renders TIFF; PNG keeps scans and edited photos lossless, only the first page is shown
        ["tiff"] = ("png", MagickFormat.Png),
        ["tif"] = ("png", MagickFormat.Png),
    };

    // ImageMagick detects the format from the content and supports far more formats than needed,
    // some of them risky (SVG, MSL, MVG, ...); only the formats the app uses are allowed
    private const string SecurityPolicy = """
        <policymap>
          <policy domain="delegate" rights="none" pattern="*" />
          <policy domain="path" rights="none" pattern="@*" />
          <policy domain="coder" rights="none" pattern="*" />
          <policy domain="coder" rights="read|write" pattern="{JPEG,JPG,JPE,JFIF,PJPEG,PNG,PNG8,PNG24,PNG32,PNG48,PNG64,GIF,GIF87,WEBP,BMP,BMP2,BMP3,TIFF,TIF,TIFF64,TGA,PBM,PGM,PPM,PNM,PAM,HEIC,HEIF,AVIF}" />
          <policy domain="coder" rights="read" pattern="{DNG,CR2,CR3,CRW,NEF,NRW,ARW,SRF,SR2,ORF,RW2,RAF,PEF,SRW,RWL,3FR,IIQ,X3F,KDC,DCR,MRW,ERF,MEF,MOS}" />
        </policymap>
        """;
    private static readonly Lazy<bool> s_initialization = new(InitializeImageMagick);

    private readonly string _outputDirectory;
    private readonly int _maxSize;

    public MagickThumbnailGenerator(string outputDirectory, int maxSize = DefaultMaxSize)
    {
        EnsureInitialized();
        _outputDirectory = Path.GetFullPath(outputDirectory);
        _maxSize = maxSize;
        Directory.CreateDirectory(_outputDirectory);
    }

    /// <summary>
    /// Applies the security policy and resource limits; has to run before ImageMagick is used for the first time.
    /// </summary>
    public static void EnsureInitialized() => _ = s_initialization.Value;

    public bool CanProcess(string extension) => s_supportedExtensions.Contains(extension.ToLowerInvariant());

    public ThumbnailResult Generate(string physicalPath, string extension)
    {
        string normalizedExtension = extension.ToLowerInvariant();
        bool convert = s_conversions.TryGetValue(normalizedExtension, out var conversion);
        using MagickImageCollection images = Load(physicalPath);
        MagickFormat outputFormat = convert ? conversion.Format : images[0].Format;
        string outputExtension = convert ? conversion.Extension : ExtensionOf(images[0].Format, normalizedExtension);
        // only GIF and WebP keep animation, other formats would be written as one file per frame
        if (outputFormat is not (MagickFormat.Gif or MagickFormat.WebP)) KeepFirstFrameOnly(images);
        // a converted file is shown as is, so the rotation goes into the pixels: libraw and TIFF only report it
        // (a portrait RAW photo would show sideways), libheif already applies it but keeps the EXIF tag,
        // which would make browsers rotate the result again
        if (convert)
        {
            foreach (var image in images) image.AutoOrient();
            ResetOrientationTag(images);
        }

        string? convertedPath = convert ? Save(images, outputExtension, outputFormat) : null;
        double ratio = Math.Min(_maxSize / (double)images[0].Width, _maxSize / (double)images[0].Height);
        if (ratio >= 1) return new ThumbnailResult(null, convertedPath);

        // truncated like before, ImageMagick's own fit would round to e.g. 638 instead of 637
        var size = new MagickGeometry((uint)(images[0].Width * ratio), (uint)(images[0].Height * ratio)) { IgnoreAspectRatio = true };
        if (images.Count > 1) images.Coalesce();
        foreach (var image in images) image.Resize(size);
        return new ThumbnailResult(Save(images, outputExtension, outputFormat), convertedPath);
    }

    private static MagickImageCollection Load(string physicalPath)
    {
        try
        {
            return new MagickImageCollection(physicalPath);
        }
        catch (MagickCorruptImageErrorException e)
        {
            throw new InvalidImageFileException(ShortMessage(e), e);
        }
        // e.g. libraw reports damaged RAW files as coder errors, the HEIC reader non-HEIC content as "image type not supported"
        catch (Exception e) when (e is MagickCoderErrorException or MagickImageErrorException)
        {
            throw new InvalidImageFileException(ShortMessage((MagickException)e), e);
        }
        catch (MagickMissingDelegateErrorException e)
        {
            throw new InvalidImageFileException("unrecognized image format", e);
        }
        catch (MagickPolicyErrorException e)
        {
            throw new InvalidImageFileException("image format not allowed", e);
        }
    }

    // ImageMagick appends the file path and its source location, e.g. "InsufficientImageDataInFile `/x/a.jpg' @ error/jpeg.c/..."
    private static string ShortMessage(MagickException e)
    {
        string message = e.Message;
        foreach (string marker in new[] { " `", " @ " })
        {
            int index = message.IndexOf(marker, StringComparison.Ordinal);
            if (index > 0) message = message[..index];
        }
        return message;
    }

    private static string ExtensionOf(MagickFormat format, string fallback) => format switch
    {
        MagickFormat.Jpeg or MagickFormat.Jpg or MagickFormat.Jpe or MagickFormat.Pjpeg => "jpg",
        MagickFormat.Png or MagickFormat.Png8 or MagickFormat.Png24 or MagickFormat.Png32 => "png",
        MagickFormat.Gif or MagickFormat.Gif87 => "gif",
        MagickFormat.WebP => "webp",
        MagickFormat.Tiff or MagickFormat.Tif or MagickFormat.Tiff64 => "tiff",
        MagickFormat.Tga => "tga",
        _ => fallback,
    };

    private static void KeepFirstFrameOnly(MagickImageCollection images)
    {
        while (images.Count > 1)
        {
            var frame = images[^1];
            images.RemoveAt(images.Count - 1);
            frame.Dispose();
        }
    }

    private static void ResetOrientationTag(MagickImageCollection images)
    {
        foreach (var image in images)
        {
            var exif = image.GetExifProfile();
            if (exif == null) continue;
            exif.SetValue(ExifTag.Orientation, (ushort)1);
            image.SetProfile(exif);
        }
    }

    private string Save(MagickImageCollection images, string extension, MagickFormat format)
    {
        // a new random file every time, so a path is never handed out before its file is fully written
        string path;
        do
        {
            path = Path.Combine(_outputDirectory, Path.ChangeExtension(Path.GetRandomFileName(), extension));
        } while (System.IO.File.Exists(path));
        images.Write(path, format);
        return path;
    }

    private static bool InitializeImageMagick()
    {
        var configFiles = ConfigurationFiles.Default;
        configFiles.Policy.Data = SecurityPolicy;
        // ImageMagick doesn't overwrite existing configuration files, so a fixed folder would keep an outdated
        // policy after an update, and in a shared temp folder another user could plant a permissive one;
        // a new private folder per process avoids both
        string configDirectory = Directory.CreateTempSubdirectory("sorter-imagemagick-").FullName;
        MagickNET.Initialize(configFiles, configDirectory);
        AppDomain.CurrentDomain.ProcessExit += (_, _) => DeleteQuietly(configDirectory);
        // hard limits against decompression bombs; there's deliberately no Time limit, it counts from the process start
        ResourceLimits.Width = 50_000;
        ResourceLimits.Height = 50_000;
        ResourceLimits.ListLength = 1_000;
        ResourceLimits.Disk = 4UL * 1024 * 1024 * 1024;
        return true;
    }

    private static void DeleteQuietly(string directory)
    {
        try
        {
            Directory.Delete(directory, recursive: true);
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}
