namespace Sorter.Data;

/// <summary>
/// Compares paths. AreSameDirectory and AreNested are meant for paths typed in the configuration
/// dialog: empty, invalid or missing directories never match, so they can be called while the user
/// is still typing. IsSamePath and IsInside work on the path text only.
/// </summary>
public static class PathComparer
{
    private static StringComparison Comparison =>
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    public static bool IsSamePath(string path1, string path2)
    {
        if (string.IsNullOrWhiteSpace(path1) || string.IsNullOrWhiteSpace(path2)) return false;
        return string.Equals(
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(path1)),
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(path2)),
            Comparison);
    }

    public static bool AreSameDirectory(string? path1, string? path2)
    {
        if (!TryGetExistingDirectory(path1, out string first) || !TryGetExistingDirectory(path2, out string second)) return false;
        return string.Equals(first, second, Comparison);
    }

    public static bool AreNested(string? path1, string? path2)
    {
        if (!TryGetExistingDirectory(path1, out string first) || !TryGetExistingDirectory(path2, out string second)) return false;
        return IsInside(first, second) || IsInside(second, first);
    }

    /// <summary>
    /// Whether the path lies anywhere below the directory. Works on the path text only,
    /// neither of them has to exist.
    /// </summary>
    public static bool IsInside(string directory, string path)
    {
        if (string.IsNullOrWhiteSpace(directory) || string.IsNullOrWhiteSpace(path)) return false;
        string parent = Path.GetFullPath(directory);
        // the separator matters: /photos/2024a is not inside /photos/2024
        string prefix = Path.EndsInDirectorySeparator(parent) ? parent : parent + Path.DirectorySeparatorChar;
        return Path.GetFullPath(path).StartsWith(prefix, Comparison);
    }

    private static bool TryGetExistingDirectory(string? path, out string fullPath)
    {
        fullPath = string.Empty;
        if (string.IsNullOrWhiteSpace(path)) return false;
        try
        {
            fullPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
        }
        catch (Exception e) when (e is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return false;
        }
        return Directory.Exists(fullPath);
    }
}
