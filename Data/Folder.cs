namespace Sorter.Data;

public class Folder : ICloneable
{
    public Folder(string path, string name)
    {
        Path = path;
        Name = name;
    }

    public string Path { get; set; }
    public string Name { get; set; }
    public char KeyBind { get; set; } = '\0';
    public string DisplayName
    {
        get
        {
            return KeyBind.Equals('\0') ? Name : string.Concat(Name, " (", KeyBind.ToString(), ")");
        }
    }
    public object Clone()
    {
        return new Folder(Path, Name) { KeyBind = KeyBind };
    }
}
