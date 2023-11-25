namespace Sorter.Data;

public class File : ICloneable
{
    private static readonly string[] s_videoExtensions = { "flv", "m3u8", "ts", "3gp", "qt", "wmv", "m4v", "mpg", "asf", "ogv", "oga", "ogx", "ogg", "spx", "webm", "avi", "mov", "mp4", "m4a", "m4p", "m4b", "m4r" }; //not sure if all of them works
    private static readonly string[] s_photoExtensions = { "gif", "jpeg", "jpg", "png", "webp", "apng", "avif" }; //supported by <img> tag
    private static readonly string[] s_textExtensions = { "txt" };
    private static readonly string[] s_pdfExtensions = { "pdf" };
    public enum FileTypeEnum
    {
        Video, Photo, Text, PDF, NotSupported
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
            if (string.IsNullOrEmpty(ThumbnailPath)) return Path;
            else return ThumbnailPath;
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
            if (s_photoExtensions.Contains(Extension.ToLower())) return FileTypeEnum.Photo;
            else if (s_videoExtensions.Contains(Extension.ToLower())) return FileTypeEnum.Video;
            else if (s_pdfExtensions.Contains(Extension.ToLower())) return FileTypeEnum.PDF;
            else if (s_textExtensions.Contains(Extension.ToLower())) return FileTypeEnum.Text;
            else return FileTypeEnum.NotSupported;
        }
    }
    public ThumbnailEnum IsThumbnailExist { get; set; }
    public object Clone()
    {
        return new File(PhysicalPath, Name, Extension, IsThumbnailExist);
    }
}
