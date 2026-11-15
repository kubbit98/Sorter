namespace Sorter.Data;

/// <summary>
/// Reads the source and destination trees. Unreadable directories are logged and skipped,
/// so one broken folder doesn't hide the rest.
/// </summary>
public class FileScanner(ILogger<FileScanner> logger)
{
    /// <summary>
    /// Files directly in a directory come before files in its subdirectories, both ordered by path.
    /// </summary>
    public List<File> ScanFiles(string sourceDirectory, IReadOnlyCollection<string> excludedDirectories, FileFilter filter)
    {
        List<File> files = [];
        try
        {
            if (!Directory.Exists(Path.GetFullPath(sourceDirectory)) || IsExcluded(sourceDirectory, excludedDirectories))
            {
                return files;
            }
            foreach (var fullPathToFile in Directory.GetFiles(sourceDirectory).OrderBy(f => f))
            {
                // original case is kept, because the extension is used to rebuild the file name on move/rename
                var extension = Path.GetExtension(fullPathToFile).Replace(".", "");
                if (!filter.IsAllowed(extension)) continue;
                files.Add(new File(fullPathToFile, Path.GetFileNameWithoutExtension(fullPathToFile), extension));
            }
            foreach (var subdirectory in Directory.GetDirectories(sourceDirectory).OrderBy(f => f))
            {
                try
                {
                    files.AddRange(ScanFiles(subdirectory, excludedDirectories, filter));
                }
                catch (Exception e)
                {
                    logger.LogError("Error with folder {Path}\n{Error}", subdirectory, e.Message);
                }
            }
        }
        catch (Exception e)
        {
            logger.LogError("Error with whole source folder {Error}", e.Message);
        }
        return files;
    }

    /// <summary>
    /// Each folder is followed by its subfolders, siblings are ordered by path.
    /// </summary>
    public List<Folder> ScanFolders(string destinationDirectory, IReadOnlyCollection<string> excludedDirectories)
    {
        List<Folder> folders = [];
        try
        {
            if (!Directory.Exists(Path.GetFullPath(destinationDirectory)))
            {
                return folders;
            }
            foreach (var subdirectory in Directory.GetDirectories(destinationDirectory).OrderBy(f => f))
            {
                try
                {
                    if (IsExcluded(subdirectory, excludedDirectories)) continue;
                    folders.Add(new Folder(subdirectory, Path.GetFileName(subdirectory)));
                    folders.AddRange(ScanFolders(subdirectory, excludedDirectories));
                }
                catch (Exception e)
                {
                    logger.LogError("Error with folder {Path}\n{Error}", subdirectory, e.Message);
                }
            }
        }
        catch (Exception e)
        {
            logger.LogError("Error with whole destination folder {Error}", e.Message);
        }
        return folders;
    }

    private static bool IsExcluded(string directory, IReadOnlyCollection<string> excludedDirectories) =>
        excludedDirectories.Any(excluded => PathComparer.IsSamePath(excluded, directory));
}
