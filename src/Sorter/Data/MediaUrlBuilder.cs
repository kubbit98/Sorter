namespace Sorter.Data;

/// <summary>
/// Maps physical paths to URLs of the static file endpoints registered in Program.cs.
/// </summary>
public class MediaUrlBuilder(string sourceDirectory, string destinationDirectory)
{
    public const string SourcePrefix = "/src/";
    public const string DestinationPrefix = "/dest/";
    public const string TempPrefix = "/tmp/";

    public static MediaUrlBuilder Empty { get; } = new(string.Empty, string.Empty);

    /// <summary>
    /// Returns the physical path unchanged when it's outside both directories.
    /// Source is checked first, so with a destination nested in the source both map to /src/.
    /// </summary>
    public string GetUrl(string physicalPath)
    {
        if (PathComparer.IsInside(sourceDirectory, physicalPath)) return Build(SourcePrefix, sourceDirectory, physicalPath);
        if (PathComparer.IsInside(destinationDirectory, physicalPath)) return Build(DestinationPrefix, destinationDirectory, physicalPath);
        return physicalPath;
    }

    /// <summary>Generated files are stored directly in the temp directory.</summary>
    public static string GetTempUrl(string physicalPath) => TempPrefix + Uri.EscapeDataString(Path.GetFileName(physicalPath));

    private static string Build(string prefix, string root, string physicalPath)
    {
        // characters like '#', '%' or '?' in file names break the URL unless escaped
        var segments = Path.GetRelativePath(root, physicalPath)
            .Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar])
            .Select(Uri.EscapeDataString);
        return prefix + string.Join('/', segments);
    }
}
