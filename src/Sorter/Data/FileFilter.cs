namespace Sorter.Data;

/// <summary>
/// Decides by extension (without the dot, any case) which source files are listed.
/// </summary>
public class FileFilter
{
    private static readonly string[] s_alwaysExcluded = ["ds_store"];

    private readonly bool _useWhiteList;
    private readonly HashSet<string> _whiteList;
    private readonly HashSet<string> _blackList;

    public static FileFilter AllowAll { get; } = new(false, [], []);

    public FileFilter(bool useWhiteList, IEnumerable<string> whiteList, IEnumerable<string> blackList)
    {
        _useWhiteList = useWhiteList;
        _whiteList = new HashSet<string>(whiteList, StringComparer.OrdinalIgnoreCase);
        _blackList = new HashSet<string>(blackList, StringComparer.OrdinalIgnoreCase);
    }

    public bool IsAllowed(string extension)
    {
        if (s_alwaysExcluded.Contains(extension, StringComparer.OrdinalIgnoreCase)) return false;
        return _useWhiteList ? _whiteList.Contains(extension) : !_blackList.Contains(extension);
    }
}
