using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Bmp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Metadata.Profiles.Exif;
using Sorter.Data;

namespace Sorter.Tests;

public class ImageSharpThumbnailGeneratorTests : ThumbnailGeneratorTests
{
    protected override IThumbnailGenerator CreateGenerator(string outputDirectory) => new ImageSharpThumbnailGenerator(outputDirectory);
}

public class MagickThumbnailGeneratorTests : ThumbnailGeneratorTests
{
    protected override IThumbnailGenerator CreateGenerator(string outputDirectory) => new MagickThumbnailGenerator(outputDirectory);
}

/// <summary>
/// The same behavior is required from every implementation, the derived classes run all tests for each of them.
/// </summary>
public abstract class ThumbnailGeneratorTests : IDisposable
{
    private readonly TempDirectory _input = new();
    private readonly TempDirectory _output = new();
    private readonly IThumbnailGenerator _generator;

    protected ThumbnailGeneratorTests()
    {
        _generator = CreateGenerator(_output.Path);
    }

    protected abstract IThumbnailGenerator CreateGenerator(string outputDirectory);

    public void Dispose()
    {
        _input.Dispose();
        _output.Dispose();
    }

    [Theory]
    [InlineData("jpg", true)]
    [InlineData("JPG", true)]
    [InlineData("png", true)]
    [InlineData("gif", true)]
    [InlineData("webp", true)]
    [InlineData("heic", true)]
    [InlineData("HEIC", true)]
    [InlineData("heif", true)]
    [InlineData("hif", true)]
    [InlineData("jfif", true)]
    [InlineData("jpe", true)]
    [InlineData("svg", false)]
    [InlineData("avif", false)]
    [InlineData("apng", false)]
    [InlineData("mp4", false)]
    [InlineData("", false)]
    public void CanProcess_IgnoresExtensionCase(string extension, bool expected)
    {
        Assert.Equal(expected, _generator.CanProcess(extension));
    }

    [Fact]
    public void SmallImage_NeedsNoThumbnail()
    {
        string path = TestImages.Create(_input.Combine("small.png"), 640, 480);

        var result = _generator.Generate(path, "png");

        Assert.Null(result.ThumbnailPath);
        Assert.Null(result.ConvertedPath);
        Assert.Empty(Directory.GetFiles(_output.Path));
    }

    [Theory]
    [InlineData(2000, 1000, 850, 425)]
    [InlineData(1000, 2000, 425, 850)]
    [InlineData(851, 851, 850, 850)]
    public void LargeImage_ThumbnailFitsMaxSizeKeepingAspectRatio(int width, int height, int expectedWidth, int expectedHeight)
    {
        string path = TestImages.Create(_input.Combine("large.jpg"), width, height);

        var result = _generator.Generate(path, "jpg");

        Assert.NotNull(result.ThumbnailPath);
        Assert.Null(result.ConvertedPath);
        var info = Image.Identify(result.ThumbnailPath!);
        Assert.Equal(expectedWidth, info.Width);
        Assert.Equal(expectedHeight, info.Height);
    }

    [Fact]
    public void Thumbnail_IsWrittenToOutputDirectory()
    {
        string path = TestImages.Create(_input.Combine("large.png"), 2000, 1000);

        var result = _generator.Generate(path, "png");

        Assert.Equal(_output.Path, Path.GetDirectoryName(result.ThumbnailPath));
        Assert.True(System.IO.File.Exists(result.ThumbnailPath));
    }

    [Fact]
    public void UppercaseExtension_ThumbnailGetsLowercaseExtension()
    {
        string path = TestImages.Create(_input.Combine("IMG_0001.JPG"), 2000, 1000);

        var result = _generator.Generate(path, "JPG");

        Assert.Equal(".jpg", Path.GetExtension(result.ThumbnailPath));
        Assert.Equal("JPEG", Image.Identify(result.ThumbnailPath!).Metadata.DecodedImageFormat!.Name);
    }

    [Fact]
    public void AnimatedGif_ThumbnailKeepsAllFrames()
    {
        string path = TestImages.CreateAnimatedGif(_input.Combine("anim.gif"), 1200, 900, frames: 3);

        var result = _generator.Generate(path, "gif");

        using var thumbnail = Image.Load(result.ThumbnailPath!);
        Assert.Equal(3, thumbnail.Frames.Count);
        Assert.Equal(850, thumbnail.Width);
    }

    [Fact]
    public void JpegWithExifOrientation_ThumbnailKeepsTagSoBrowserRotatesIt()
    {
        string path = TestImages.Create(_input.Combine("portrait.jpg"), 2000, 1500, exifOrientation: 6);

        var result = _generator.Generate(path, "jpg");

        var info = Image.Identify(result.ThumbnailPath!);
        Assert.Equal(850, info.Width);
        Assert.Equal(637, info.Height);
        Assert.True(info.Metadata.ExifProfile!.TryGetValue(ExifTag.Orientation, out var orientation));
        Assert.Equal((ushort)6, orientation.Value);
    }

    [Fact]
    public void SmallHeic_IsConvertedToJpgWithoutThumbnail()
    {
        var result = _generator.Generate(TestImages.TestDataPath("small_640x480.heic"), "heic");

        Assert.Null(result.ThumbnailPath);
        Assert.NotNull(result.ConvertedPath);
        Assert.Equal(".jpg", Path.GetExtension(result.ConvertedPath));
        var info = Image.Identify(result.ConvertedPath!);
        Assert.Equal("JPEG", info.Metadata.DecodedImageFormat!.Name);
        Assert.Equal(640, info.Width);
        Assert.Equal(480, info.Height);
    }

    [Fact]
    public void UppercaseHeicExtension_IsConvertedToo()
    {
        var result = _generator.Generate(TestImages.TestDataPath("small_640x480.heic"), "HEIC");

        Assert.NotNull(result.ConvertedPath);
    }

    [Theory]
    [InlineData("heif")]
    [InlineData("hif")]
    [InlineData("HIF")]
    public void OtherHeifExtensions_AreConvertedLikeHeic(string extension)
    {
        string path = _input.Combine($"camera.{extension}");
        System.IO.File.Copy(TestImages.TestDataPath("small_640x480.heic"), path);

        var result = _generator.Generate(path, extension);

        Assert.Equal(".jpg", Path.GetExtension(result.ConvertedPath));
        Assert.Equal(640, Image.Identify(result.ConvertedPath!).Width);
    }

    [Theory]
    [InlineData("jfif")]
    [InlineData("jpe")]
    public void JpegExtensionVariants_GetJpgThumbnail(string extension)
    {
        string path = _input.Combine($"photo.{extension}");
        TestImages.Create(path, 2000, 1000, format: "jpg");

        var result = _generator.Generate(path, extension);

        Assert.Null(result.ConvertedPath);
        Assert.Equal(".jpg", Path.GetExtension(result.ThumbnailPath));
        Assert.Equal("JPEG", Image.Identify(result.ThumbnailPath!).Metadata.DecodedImageFormat!.Name);
    }

    [Fact]
    public void FileWithMismatchedExtension_ThumbnailUsesDecodedFormat()
    {
        string path = _input.Combine("actually_png.jpg");
        TestImages.Create(path, 2000, 1000, format: "png");

        var result = _generator.Generate(path, "jpg");

        Assert.Equal(".png", Path.GetExtension(result.ThumbnailPath));
        Assert.Equal("PNG", Image.Identify(result.ThumbnailPath!).Metadata.DecodedImageFormat!.Name);
    }

    [Fact]
    public void RotatedHeic_IsConvertedUprightWithoutOrientationTag()
    {
        var result = _generator.Generate(TestImages.TestDataPath("portrait_1200x1600_irot_exif6.heic"), "heic");

        // libheif applies the rotation, an orientation tag left in the JPG would make browsers rotate it again
        var converted = Image.Identify(result.ConvertedPath!);
        Assert.Equal(1200, converted.Width);
        Assert.Equal(1600, converted.Height);
        ushort? orientation = converted.Metadata.ExifProfile?.TryGetValue(ExifTag.Orientation, out var value) == true ? value.Value : null;
        Assert.True(orientation is null or 1, $"Unexpected EXIF orientation {orientation}");

        var thumbnail = Image.Identify(result.ThumbnailPath!);
        Assert.Equal(637, thumbnail.Width);
        Assert.Equal(850, thumbnail.Height);
    }

    [Fact]
    public void SmallBmp_IsConvertedToPngWithoutThumbnail()
    {
        string path = TestImages.Create(_input.Combine("small.bmp"), 640, 480);

        var result = _generator.Generate(path, "bmp");

        Assert.Null(result.ThumbnailPath);
        Assert.Equal(".png", Path.GetExtension(result.ConvertedPath));
        var info = Image.Identify(result.ConvertedPath!);
        Assert.Equal("PNG", info.Metadata.DecodedImageFormat!.Name);
        Assert.Equal(640, info.Width);
        Assert.Equal(480, info.Height);
    }

    [Fact]
    public void LargeBmp_IsConvertedAndGetsPngThumbnail()
    {
        string path = TestImages.Create(_input.Combine("IMAGE.BMP"), 2000, 1000);

        var result = _generator.Generate(path, "BMP");

        Assert.Equal(2000, Image.Identify(result.ConvertedPath!).Width);
        var thumbnail = Image.Identify(result.ThumbnailPath!);
        Assert.Equal("PNG", thumbnail.Metadata.DecodedImageFormat!.Name);
        Assert.Equal(850, thumbnail.Width);
    }

    [Fact]
    public void BmpWithAlphaChannel_KeepsTransparencyInPng()
    {
        // 32-bit BMP with alpha is one of the variants browsers don't display reliably
        string path = _input.Combine("alpha.bmp");
        using (var image = new Image<Rgba32>(100, 100, new Rgba32(255, 0, 0, 255)))
        {
            image[0, 0] = new Rgba32(0, 0, 0, 0);
            image.Save(path, new BmpEncoder { BitsPerPixel = BmpBitsPerPixel.Pixel32, SupportTransparency = true });
        }

        var result = _generator.Generate(path, "bmp");

        using var converted = Image.Load<Rgba32>(result.ConvertedPath!);
        Assert.Equal(0, converted[0, 0].A);
        Assert.Equal(255, converted[50, 50].A);
    }

    [Theory]
    [InlineData("broken.jpg", "jpg")]
    [InlineData("broken.heic", "heic")]
    public void FileWithInvalidContent_ThrowsInvalidImageFileException(string fileName, string extension)
    {
        string path = _input.CreateFile(fileName, "This is not an image.");

        var exception = Assert.Throws<InvalidImageFileException>(() => _generator.Generate(path, extension));
        Assert.False(string.IsNullOrWhiteSpace(exception.Message));
        // the message ends up in a log line next to the path already
        Assert.DoesNotContain(path, exception.Message);
    }

    [Fact]
    public void SvgDisguisedAsJpg_IsRejected()
    {
        // a library detecting the format from the content mustn't hand this to an SVG renderer
        string path = _input.CreateFile("vector.jpg",
            "<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"2000\" height=\"1000\"><rect width=\"2000\" height=\"1000\"/></svg>");

        Assert.Throws<InvalidImageFileException>(() => _generator.Generate(path, "jpg"));
    }

    [Fact]
    public void TruncatedHeic_ThrowsInvalidImageFileException()
    {
        byte[] content = System.IO.File.ReadAllBytes(TestImages.TestDataPath("portrait_1200x1600_irot_exif6.heic"));
        string path = _input.Combine("truncated.heic");
        System.IO.File.WriteAllBytes(path, content[..(content.Length / 2)]);

        var exception = Assert.Throws<InvalidImageFileException>(() => _generator.Generate(path, "heic"));
        Assert.StartsWith("Invalid input", exception.Message);
    }
}
