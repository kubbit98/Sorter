namespace Sorter.Data;

public class File
{
    public static string[] s_videoExtensions = { "flv", "m3u8", "ts", "3gp", "qt", "wmv", "m4v", "mpg", "asf", "ogv", "oga", "ogx", "ogg", "spx", "webm", "avi", "mov", "mp4", "m4a", "m4p", "m4b", "m4r" }; //not sure if all of them works
    public static string[] s_photoExtensions = { "gif", "jpeg", "jpg", "png", "webp", "apng", "avif" }; //supported by <img> tag
    public static string[] s_textExtensions = { "txt" };
    public static string[] s_pdfExtensions = { "pdf" };
    public enum FileTypeEnum
    {
        Video, Photo, Text, PDF, NotSupported
    }
    public File(string physicalPath, string name, string extension)
    {
        Path = physicalPath;
        PhysicalPath = physicalPath;
        Name = name;
        Extension = extension;
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
                return path.Substring(0, path.LastIndexOf(System.IO.Path.DirectorySeparatorChar)) + System.IO.Path.DirectorySeparatorChar;
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
    //public string VideoType //i used this to type in video source
    //{
    //    get
    //    {
    //        switch (Extension)
    //        {
    //            case "flv":
    //                return "video/x-flv";
    //            case "m3u8":
    //                return "application/x-mpegURL";
    //            case "ts":
    //                return "video/MP2T";
    //            case "3gp":
    //                return "video/3gpp";
    //            case "qt":
    //                return "video/quicktime";
    //            case "wmv":
    //                return "video/x-ms-wmv";
    //            case "m4v":
    //                return "video/x-m4v";
    //            case "mpg":
    //                return "video/mpeg";
    //            case "asf":
    //                return "video/x-ms-asf";
    //            case "ogv":
    //            case "oga":
    //            case "ogx":
    //            case "ogg":
    //            case "spx":
    //                return "video/ogg";
    //            case "webm":
    //                return "video/webm";
    //            case "avi":
    //            case "mov":
    //            case "mp4":
    //            case "m4a":
    //            case "m4p":
    //            case "m4b":
    //            case "m4r":
    //                return "video/mp4";
    //            default:
    //                return "video/mp4";
    //        }
    //    }
    //}

}
