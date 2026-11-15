using System.Diagnostics.CodeAnalysis;

namespace Sorter.Data;

/// <summary>
/// Settings of one loaded session, taken from config.json (ConfigOptions) and appsettings.json.
/// All paths are absolute.
/// </summary>
public sealed class SorterSettings
{
    public string Source { get; }
    public string Destination { get; }
    public IReadOnlyList<string> ExcludedSourceDirectories { get; }
    public IReadOnlyList<string> ExcludedDestinationDirectories { get; }
    public FileFilter Filter { get; }
    public bool UseThumbnails { get; }

    public static SorterSettings Empty { get; } = new(string.Empty, string.Empty, [], [], FileFilter.AllowAll, false);

    private SorterSettings(string source, string destination, IReadOnlyList<string> excludedSourceDirectories,
        IReadOnlyList<string> excludedDestinationDirectories, FileFilter filter, bool useThumbnails)
    {
        Source = source;
        Destination = destination;
        ExcludedSourceDirectories = excludedSourceDirectories;
        ExcludedDestinationDirectories = excludedDestinationDirectories;
        Filter = filter;
        UseThumbnails = useThumbnails;
    }

    /// <summary>
    /// Fails when the source or destination is empty, invalid or doesn't exist.
    /// </summary>
    public static bool TryCreate(ConfigOptions options, IConfiguration configuration, [NotNullWhen(true)] out SorterSettings? settings)
    {
        settings = null;
        if (!TryGetExistingDirectory(options.Source, out string source)) return false;
        if (!TryGetExistingDirectory(options.Destination, out string destination)) return false;

        settings = new SorterSettings(
            source,
            destination,
            ToFullPaths(options.ExcludeDirsSource),
            ToFullPaths(options.ExcludeDirsDestination),
            // lists can be null when config.json contains e.g. "WhiteList": null
            new FileFilter(options.UseWhiteListInsteadOfBlackList, options.WhiteList ?? [], options.BlackList ?? []),
            configuration.GetValue<bool>("UseThumbnails"));
        return true;
    }

    private static bool TryGetExistingDirectory(string? path, out string fullPath)
    {
        fullPath = string.Empty;
        if (string.IsNullOrWhiteSpace(path)) return false;
        try
        {
            fullPath = Path.GetFullPath(path);
        }
        catch (Exception e) when (e is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return false;
        }
        return Directory.Exists(fullPath);
    }

    /// <summary>Absolute paths; null lists and blank entries (possible in hand-edited config.json) are skipped.</summary>
    public static string[] ToFullPaths(string[]? paths) =>
        (paths ?? []).Where(path => !string.IsNullOrWhiteSpace(path)).Select(Path.GetFullPath).ToArray();
}
