namespace Sorter.Data;

public class File
{
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
            if (!string.IsNullOrEmpty(Name)) path = path.Replace(Name, "");
            if (!string.IsNullOrEmpty(Extension)) path = path.Replace(Extension, "");
            return path;
        }
    }
    
}
