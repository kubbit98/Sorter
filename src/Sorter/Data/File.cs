namespace Sorter.Data;

public class File : ICloneable
{
    // checked to play in Firefox and Chromium; FLV, MPEG-TS, MPEG-PS, AVI, WMV/ASF, 3GP (H.263) and Theora (OGV) don't
    private static readonly string[] s_videoExtensions = ["mp4", "m4v", "mov", "qt", "webm"];
    private static readonly string[] s_photoExtensions = ["gif", "jpeg", "jpg", "jfif", "jpe", "png", "webp", "apng", "avif", "heic", "heif", "hif", "bmp", "svg", "tiff", "tif"];
    // camera RAW files, shown as a JPG conversion
    private static readonly string[] s_rawPhotoExtensions = ["dng", "cr2", "cr3", "crw", "nef", "nrw", "arw", "srf", "sr2", "orf", "rw2", "raf", "pef", "srw", "rwl", "3fr", "iiq", "x3f", "kdc", "dcr", "mrw", "erf", "mef", "mos"];
    // m4a, m4b (audiobooks) and m4r (ringtones) are AAC; DRM protected m4p and Speex can't be played
    private static readonly string[] s_audioExtensions = ["mp3", "wav", "ogg", "oga", "m4a", "m4b", "m4r"];
    private static readonly string[] s_textExtensions = ["txt", "csv", "log", "md", "html", "js", "css"];
    private static readonly string[] s_pdfExtensions = ["pdf"];
    public enum FileTypeEnum
    {
        Video, Photo, Audio, Text, PDF, NotSupported
    }
    public enum ThumbnailEnum
    {
        Exists, WillBe, WillNot, Unknown
    }
    public File(string physicalPath, string name, string extension)
    {
        Path = physicalPath;
        PhysicalPath = physicalPath;
        Name = name;
        Extension = extension;
        IsThumbnailExist = ThumbnailEnum.Unknown;
    }
    public File(string physicalPath, string name, string extension, ThumbnailEnum isThumbnailExist)
    {
        Path = physicalPath;
        PhysicalPath = physicalPath;
        Name = name;
        Extension = extension;
        IsThumbnailExist = isThumbnailExist;
    }

    public string Path { get; set; }
    public string PhysicalPath { get; set; }
    public string? ThumbnailPath { get; set; }
    public string? ConvertedPath { get; set; }
    public string Name { get; set; }
    public string Extension { get; set; }
    public int? FIndex { get; set; }
    public string OnlyPath
    {
        get
        {
            string path = PhysicalPath;
            try
            {
                return path[..path.LastIndexOf(System.IO.Path.DirectorySeparatorChar)] + System.IO.Path.DirectorySeparatorChar;
            }
            catch
            {
                if (!string.IsNullOrEmpty(Name)) path = path.Replace(Name, "");
                if (!string.IsNullOrEmpty(Extension)) path = path.Replace(Extension, "");
                return path;
            }
        }
    }
    public string DisplayPath
    {
        get
        {
            if (!string.IsNullOrEmpty(ThumbnailPath)) return ThumbnailPath;
            else return ZoomPath;
        }
    }
    public string ZoomPath
    {
        get
        {
            if (string.IsNullOrEmpty(ConvertedPath)) return Path;
            else return ConvertedPath;
        }
    }
    public string NameWithExtension
    {
        get
        {
            if (string.IsNullOrWhiteSpace(Extension)) return Name;
            else return Name + "." + Extension;
        }
    }
    public FileTypeEnum FileType
    {
        get
        {
            if (s_photoExtensions.Contains(Extension.ToLower()) || s_rawPhotoExtensions.Contains(Extension.ToLower())) return FileTypeEnum.Photo;
            else if (s_videoExtensions.Contains(Extension.ToLower())) return FileTypeEnum.Video;
            else if (s_audioExtensions.Contains(Extension.ToLower())) return FileTypeEnum.Audio;
            else if (s_pdfExtensions.Contains(Extension.ToLower())) return FileTypeEnum.PDF;
            else if (s_textExtensions.Contains(Extension.ToLower())) return FileTypeEnum.Text;
            else return FileTypeEnum.NotSupported;
        }
    }
    public ThumbnailEnum IsThumbnailExist { get; set; }
    public object Clone()
    {
        File clone = new(PhysicalPath, Name, Extension, IsThumbnailExist)
        {
            ThumbnailPath = ThumbnailPath,
            ConvertedPath = ConvertedPath
        };
        return clone;
    }
}
