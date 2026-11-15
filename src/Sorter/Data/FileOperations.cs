namespace Sorter.Data;

/// <summary>
/// File system operations on user files. Failures are logged and rethrown.
/// </summary>
public class FileOperations(ILogger<FileOperations> logger)
{
    // about a screen of text; more only made the page longer than the window
    public const int MaxTextPreviewLength = 2000;

    public static string CombineFileName(string name, string extension) =>
        string.IsNullOrEmpty(extension) ? name : string.Concat(name, ".", extension);

    public bool IsNameFree(string directory, string fileName) => !System.IO.File.Exists(Path.Combine(directory, fileName));

    /// <summary>Moves the file into the directory under the same name and returns its new path.</summary>
    public string Move(string physicalPath, string targetDirectory)
    {
        string fileName = Path.GetFileName(physicalPath);
        string newPath = Path.Combine(targetDirectory, fileName);
        try
        {
            System.IO.File.Move(physicalPath, newPath);
            logger.LogInformation("File {FileNameExt} moved to {Destination}", fileName, targetDirectory);
            return newPath;
        }
        catch (Exception e)
        {
            logger.LogError("File {FileNameExt} cannot be moved to {Destination}\n{Error}", fileName, targetDirectory, e.Message);
            throw;
        }
    }

    /// <summary>
    /// Renames the file keeping its extension and returns its new path, or null when the name isn't valid.
    /// </summary>
    /// <exception cref="InvalidOperationException">Another file already has that name.</exception>
    public string? Rename(string physicalPath, string newName, string extension)
    {
        if (!IsValidFileName(newName)) return null;
        string newPath = Path.Combine(Path.GetDirectoryName(physicalPath)!, CombineFileName(newName, extension));
        if (string.Equals(newPath, physicalPath, StringComparison.Ordinal)) return physicalPath;
        // on Windows a change of letter case only is still the same file
        bool caseOnlyChange = OperatingSystem.IsWindows() && string.Equals(newPath, physicalPath, StringComparison.OrdinalIgnoreCase);
        if (!caseOnlyChange && System.IO.File.Exists(newPath)) throw new InvalidOperationException("File with that name exists");
        try
        {
            System.IO.File.Move(physicalPath, newPath);
            logger.LogInformation("File renamed from {OldName} to {NewName}", Path.GetFileName(physicalPath), Path.GetFileName(newPath));
            return newPath;
        }
        catch (Exception e)
        {
            logger.LogError("File {FileNameExt} cannot be renamed to {NewFileName}\n{Error}", Path.GetFileName(physicalPath), newName, e.Message);
            throw;
        }
    }

    /// <summary>Creates the folder (or uses an existing one) and returns its path, or null when the name isn't valid.</summary>
    public string? CreateFolder(string parentDirectory, string folderName)
    {
        if (!IsValidFileName(folderName)) return null;
        string path = Path.Combine(parentDirectory, folderName);
        Directory.CreateDirectory(path);
        return path;
    }

    /// <summary>Reads at most MaxTextPreviewLength characters, longer content ends with "[...]".</summary>
    public string ReadTextPreview(string physicalPath)
    {
        // only the beginning is read, so a huge log file isn't loaded into memory
        using var reader = new StreamReader(physicalPath);
        char[] buffer = new char[MaxTextPreviewLength + 1];
        int read = reader.ReadBlock(buffer, 0, buffer.Length);
        return read > MaxTextPreviewLength
            ? string.Concat(new string(buffer, 0, MaxTextPreviewLength), "[...]")
            : new string(buffer, 0, read);
    }

    /// <summary>
    /// Whether the name can be used for a file or folder; the UI validates with the same rule.
    /// "." and ".." would point at the current or parent directory instead.
    /// </summary>
    public static bool IsValidFileName(string? name) =>
        !string.IsNullOrWhiteSpace(name) && name != "." && name != ".." && name.IndexOfAny(Path.GetInvalidFileNameChars()) == -1;
}
