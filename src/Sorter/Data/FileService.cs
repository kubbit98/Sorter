using Microsoft.Extensions.Options;

namespace Sorter.Data
{
    /// <summary>
    /// Session state shared by all browser sessions (the service is a singleton). All fields below
    /// _stateLock are read and written only under it; slow disk work runs outside the lock, so one
    /// session moving a large file doesn't block the others. The file list is never modified after
    /// a scan, only replaced, so a reference taken under the lock can be read outside of it.
    /// Thumbnail fields of the files are written by ThumbnailScheduler without the lock, single
    /// reference writes can't be torn.
    /// </summary>
    public class FileService
    {
        private readonly IConfiguration _configuration;
        private readonly IOptionsMonitor<ConfigOptions> _options;
        private readonly IOptionsMonitor<KeyBindsOptions> _keyOptions;
        private readonly ConfigOptionsService _configOptionsService;
        private readonly ILogger _logger;
        private readonly ThumbnailScheduler _thumbnailScheduler;
        private readonly FileScanner _fileScanner;
        private readonly FileOperations _fileOperations;
        private readonly string password;

        // the first file is shown right after initialization, a stuck decoder mustn't block the session though
        private static readonly TimeSpan s_firstThumbnailTimeout = TimeSpan.FromSeconds(30);
        // initialization awaits, so it can't hold _stateLock; this keeps two of them from running at once
        private readonly SemaphoreSlim _initializationLock = new(1, 1);

        private readonly object _stateLock = new();
        private IReadOnlyList<File> files = [];
        private List<Folder> folders = [];
        private int indexOfLastProcessingFile = -1;
        private string sessionStoragePrefix;
        private SorterSettings settings = SorterSettings.Empty;
        private MediaUrlBuilder urlBuilder = MediaUrlBuilder.Empty;
        private bool initialized;

        public FileService(IConfiguration configuration, IOptionsMonitor<ConfigOptions> options, IOptionsMonitor<KeyBindsOptions> keyOptions, ConfigOptionsService configOptionsService, ILogger<FileService> logger, ThumbnailScheduler thumbnailScheduler, FileScanner fileScanner, FileOperations fileOperations)
        {
            _fileOperations = fileOperations;
            _fileScanner = fileScanner;
            _thumbnailScheduler = thumbnailScheduler;
            _configuration = configuration;
            _options = options;
            _keyOptions = keyOptions;
            _configOptionsService = configOptionsService;
            _logger = logger;
            password = _configuration.GetValue<string>("Password") ?? string.Empty;
            sessionStoragePrefix = Path.GetRandomFileName();
        }
        public async Task<bool> InitializeFiles(bool reInit)
        {
            await _initializationLock.WaitAsync();
            try
            {
                lock (_stateLock)
                {
                    if (initialized && !reInit) return true;
                }
                // config.json is reloaded asynchronously after the configuration dialog saves it
                while (!_configOptionsService.CheckMonitor(_options.CurrentValue)) await Task.Delay(100);
                if (!SorterSettings.TryCreate(_options.CurrentValue, _configuration, out var newSettings)) return false;

                // scanning reads the disk, other sessions keep using the previous lists until the swap below
                var newFiles = _fileScanner.ScanFiles(newSettings.Source, newSettings.ExcludedSourceDirectories, newSettings.Filter);
                var newFolders = _fileScanner.ScanFolders(newSettings.Destination, newSettings.ExcludedDestinationDirectories);
                lock (_stateLock)
                {
                    settings = newSettings;
                    urlBuilder = new MediaUrlBuilder(newSettings.Source, newSettings.Destination);
                    files = newFiles;
                    folders = newFolders;
                    indexOfLastProcessingFile = -1;
                    AssignKeyBinds();
                }

                _thumbnailScheduler.Reset();
                if (newSettings.UseThumbnails && newFiles.Count > 0)
                {
                    _thumbnailScheduler.ScheduleAhead(newFiles, 0);
                    await Task.WhenAny(_thumbnailScheduler.Schedule(newFiles[0]), Task.Delay(s_firstThumbnailTimeout));
                }

                lock (_stateLock)
                {
                    initialized = true;
                    if (reInit) sessionStoragePrefix = Path.GetRandomFileName();
                }
                return true;
            }
            finally
            {
                _initializationLock.Release();
            }
        }
        public Task<string> GetStoragePrefix()
        {
            lock (_stateLock) return Task.FromResult(sessionStoragePrefix);
        }
        public Task<string> GetPassword()
        {
            return Task.FromResult(password);
        }
        public Task<bool> GetAllowRename()
        {
            // read on every call, so changing it in the configuration doesn't need a new session
            return Task.FromResult(_options.CurrentValue.AllowRename);
        }
        public Task<Folder[]> GetFoldersAsync()
        {
            lock (_stateLock) return Task.FromResult(folders.ToArray());
        }
        /// <summary>
        /// Hands out the next file not yet shown in any session, so sessions split the files between them.
        /// </summary>
        public int? GetNextIndex()
        {
            int index;
            IReadOnlyList<File> currentFiles;
            bool useThumbnails;
            lock (_stateLock)
            {
                if (indexOfLastProcessingFile >= files.Count - 1) return null;
                index = ++indexOfLastProcessingFile;
                currentFiles = files;
                useThumbnails = settings.UseThumbnails;
            }
            if (useThumbnails) _thumbnailScheduler.ScheduleAhead(currentFiles, index);
            return index;
        }
        public File? GetFileAtIndex(int index)
        {
            lock (_stateLock)
            {
                if (index < 0 || index >= files.Count) return null;
                File file = (File)files[index].Clone();
                file.Path = urlBuilder.GetUrl(file.PhysicalPath);
                file.FIndex = index;
                return file;
            }
        }
        public File? GetFileAtIndex(int index, string name)
        {
            File? file = GetFileAtIndex(index);
            if (file == null) return null;
            if (!file.Name.Equals(name)) throw new Exception("You need to reload yor session, the server was reset");
            return file;
        }
        public void AppendDirectoriesWithKeyBinds()
        {
            lock (_stateLock) AssignKeyBinds();
        }
        // callers hold _stateLock
        private void AssignKeyBinds()
        {
            var dict = _keyOptions.CurrentValue.KeyBinds;
            foreach (var folder in folders)
            {
                if (dict.ContainsValue(folder.Path))
                {
                    folder.KeyBind = dict.FirstOrDefault(x => x.Value == folder.Path).Key.First();
                }
                else
                {
                    folder.KeyBind = '\0';
                }
            }
        }
        public Task<bool> IsDestinationAvailable(File file, string destiny)
        {
            return Task.FromResult(_fileOperations.IsNameFree(destiny, file.NameWithExtension));
        }
        public void MoveFile(File file, string destiny)
        {
            int index = file.FIndex!.Value;
            IReadOnlyList<File> currentFiles;
            lock (_stateLock)
            {
                currentFiles = files;
                if (currentFiles[index].Name != file.Name)
                {
                    _logger.LogWarning("The file index has changed, you need to reload session");
                    return;
                }
            }
            // moving to another disk copies the whole file, which mustn't block other sessions
            string newPath = _fileOperations.Move(file.PhysicalPath, destiny);
            file.PhysicalPath = newPath;
            lock (_stateLock) currentFiles[index].PhysicalPath = newPath;
        }
        public Task<string> GetTextFileContent(File file)
        {
            if (file == null) throw new Exception("No file");
            return Task.FromResult(_fileOperations.ReadTextPreview(file.PhysicalPath));
        }
        public bool CreateFolder(string folderName)
        {
            // creating a directory is quick, holding the lock keeps the duplicate check and adding atomic
            lock (_stateLock)
            {
                if (folders.Any(f => f.Name == folderName)) return false;
                string? fullPath = _fileOperations.CreateFolder(settings.Destination, folderName);
                if (fullPath == null) return false;
                folders.Add(new Folder(fullPath, Path.GetFileName(fullPath)));
                return true;
            }
        }
        public void ChangeFileName(int index, string newFileName)
        {
            File file;
            string physicalPath;
            string extension;
            MediaUrlBuilder currentUrlBuilder;
            lock (_stateLock)
            {
                file = files[index];
                physicalPath = file.PhysicalPath;
                extension = file.Extension;
                currentUrlBuilder = urlBuilder;
            }
            string? newFullPath = _fileOperations.Rename(physicalPath, newFileName, extension);
            if (newFullPath == null) return;
            // all fields at once, so GetFileAtIndex never copies a half renamed file
            lock (_stateLock)
            {
                file.PhysicalPath = newFullPath;
                file.Name = newFileName;
                file.Path = currentUrlBuilder.GetUrl(newFullPath);
            }
        }
    }
}
