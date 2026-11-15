using Sorter.Data;
using SorterFile = Sorter.Data.File;

namespace Sorter.Tests;

public class FileTests
{
    [Theory]
    [InlineData("jpg", SorterFile.FileTypeEnum.Photo)]
    [InlineData("JPG", SorterFile.FileTypeEnum.Photo)]
    [InlineData("heic", SorterFile.FileTypeEnum.Photo)]
    [InlineData("HEIC", SorterFile.FileTypeEnum.Photo)]
    [InlineData("heif", SorterFile.FileTypeEnum.Photo)]
    [InlineData("hif", SorterFile.FileTypeEnum.Photo)]
    [InlineData("jfif", SorterFile.FileTypeEnum.Photo)]
    [InlineData("jpe", SorterFile.FileTypeEnum.Photo)]
    [InlineData("svg", SorterFile.FileTypeEnum.Photo)]
    [InlineData("apng", SorterFile.FileTypeEnum.Photo)]
    [InlineData("mp4", SorterFile.FileTypeEnum.Video)]
    [InlineData("ogg", SorterFile.FileTypeEnum.Video)] // listed as both video and audio, video wins
    [InlineData("m4a", SorterFile.FileTypeEnum.Video)]
    [InlineData("mp3", SorterFile.FileTypeEnum.Audio)]
    [InlineData("pdf", SorterFile.FileTypeEnum.PDF)]
    [InlineData("txt", SorterFile.FileTypeEnum.Text)]
    [InlineData("md", SorterFile.FileTypeEnum.Text)]
    [InlineData("bmp", SorterFile.FileTypeEnum.Photo)]
    [InlineData("BMP", SorterFile.FileTypeEnum.Photo)]
    [InlineData("tiff", SorterFile.FileTypeEnum.Photo)]
    [InlineData("TIF", SorterFile.FileTypeEnum.Photo)]
    [InlineData("mkv", SorterFile.FileTypeEnum.NotSupported)]
    [InlineData("", SorterFile.FileTypeEnum.NotSupported)]
    public void FileType_IsDetectedFromExtensionIgnoringCase(string extension, SorterFile.FileTypeEnum expected)
    {
        var file = new SorterFile("/x/file", "file", extension);

        Assert.Equal(expected, file.FileType);
    }

    [Theory]
    [InlineData("photo", "JPG", "photo.JPG")]
    [InlineData("README", "", "README")]
    [InlineData("", "txt", ".txt")]
    public void NameWithExtension_KeepsOriginalCase(string name, string extension, string expected)
    {
        var file = new SorterFile("/x/file", name, extension);

        Assert.Equal(expected, file.NameWithExtension);
    }

    [Fact]
    public void DisplayPath_PrefersThumbnailThenConversionThenOriginal()
    {
        var file = new SorterFile("/x/a.heic", "a", "heic") { Path = "/src/a.heic" };
        Assert.Equal("/src/a.heic", file.DisplayPath);

        file.ConvertedPath = "/tmp/converted.jpg";
        Assert.Equal("/tmp/converted.jpg", file.DisplayPath);

        file.ThumbnailPath = "/tmp/thumb.jpg";
        Assert.Equal("/tmp/thumb.jpg", file.DisplayPath);
    }

    [Fact]
    public void ZoomPath_UsesConversionButNeverThumbnail()
    {
        var file = new SorterFile("/x/a.jpg", "a", "jpg") { Path = "/src/a.jpg", ThumbnailPath = "/tmp/thumb.jpg" };
        Assert.Equal("/src/a.jpg", file.ZoomPath);

        file.ConvertedPath = "/tmp/converted.jpg";
        Assert.Equal("/tmp/converted.jpg", file.ZoomPath);
    }

    [Fact]
    public void Clone_CopiesGeneratedPathsAndState()
    {
        var file = new SorterFile("/x/a.heic", "a", "heic", SorterFile.ThumbnailEnum.Exists)
        {
            ThumbnailPath = "/tmp/thumb.jpg",
            ConvertedPath = "/tmp/converted.jpg"
        };

        var clone = (SorterFile)file.Clone();

        Assert.NotSame(file, clone);
        Assert.Equal(file.ThumbnailPath, clone.ThumbnailPath);
        Assert.Equal(file.ConvertedPath, clone.ConvertedPath);
        Assert.Equal(SorterFile.ThumbnailEnum.Exists, clone.IsThumbnailExist);
    }
}
