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
    public string Name { get; set; }
    public string Extension { get; set; }
    public int? FIndex { get; set; }
    
}
