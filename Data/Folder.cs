namespace Sorter.Data;

public class Folder
{
    public Folder(string path, string name)
    {
        Path = path;
        Name = name;
    }

    public string Path { get; set; }
    public string Name { get; set; }
}
