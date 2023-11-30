namespace Sorter.Data;

public class Folder : ICloneable
{
    private static readonly int s_maxDisplayNameLength = 42;
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
            string shortName = Name.Length > s_maxDisplayNameLength ? string.Concat(Name[..s_maxDisplayNameLength], "[...]") : Name;
            return KeyBind.Equals('\0') ? shortName : string.Concat(shortName, " (", KeyBind.ToString(), ")");
        }
    }
    public object Clone()
    {
        return new Folder(Path, Name) { KeyBind = KeyBind };
    }
}
