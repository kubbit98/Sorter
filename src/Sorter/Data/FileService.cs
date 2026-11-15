using Microsoft.Extensions.Options;

namespace Sorter.Data
{
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

        // the first file is shown right after initialization, a stuck decoder mustn't block the session though
        private static readonly TimeSpan s_firstThumbnailTimeout = TimeSpan.FromSeconds(30);
        // two sessions initializing at once would replace each other's file list halfway
        private readonly SemaphoreSlim _initializationLock = new(1, 1);

        private List<File> files = [];
        private List<Folder> folders = [];
        private int indexOfLastProcessingFile = -1;
        private string sessionStoragePrefix;

        private SorterSettings settings = SorterSettings.Empty;
        private MediaUrlBuilder urlBuilder = MediaUrlBuilder.Empty;
        private readonly string password;
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
            initialized = false;
        }
        public async Task<bool> InitializeFiles(bool reInit)
        {
            await _initializationLock.WaitAsync();
            try
            {
                if (initialized && !reInit) return true;
                // config.json is reloaded asynchronously after the configuration dialog saves it
                while (!_configOptionsService.CheckMonitor(_options.CurrentValue)) await Task.Delay(100);
                if (!SorterSettings.TryCreate(_options.CurrentValue, _configuration, out var newSettings)) return false;
                settings = newSettings;
                urlBuilder = new MediaUrlBuilder(settings.Source, settings.Destination);
                _thumbnailScheduler.Reset();
                LoadFilesAndFolders();
                indexOfLastProcessingFile = -1;
                if (settings.UseThumbnails && files.Count > 0)
                {
                    _thumbnailScheduler.ScheduleAhead(files, 0);
                    await Task.WhenAny(_thumbnailScheduler.Schedule(files[0]), Task.Delay(s_firstThumbnailTimeout));
                }
                initialized = true;
                if (reInit) sessionStoragePrefix = Path.GetRandomFileName();
                return true;
            }
            finally
            {
                _initializationLock.Release();
            }
        }
        public Task<string> GetStoragePrefix()
        {
            return Task.FromResult(sessionStoragePrefix);
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
            return Task.FromResult(folders.ToArray());
        }
        public int? GetNextIndex()
        {
            if (indexOfLastProcessingFile < files.Count - 1)
            {
                indexOfLastProcessingFile++;
                if (settings.UseThumbnails) _thumbnailScheduler.ScheduleAhead(files, indexOfLastProcessingFile);
                return indexOfLastProcessingFile;
            }
            return null;
        }
        public File? GetFileAtIndex(int index)
        {
            if (index < files.Count && index >= 0)
            {
                File file = (File)files[index].Clone();
                file.Path = urlBuilder.GetUrl(file.PhysicalPath);
                file.FIndex = index;
                return file;
            }
            return null;
        }
        public File? GetFileAtIndex(int index, string name)
        {
            File? file = GetFileAtIndex(index);
            if (file == null) return null;
            if (!file.Name.Equals(name)) throw new Exception("You need to reload yor session, the server was reset");
            return file;
        }
        private void LoadFilesAndFolders()
        {
            files = _fileScanner.ScanFiles(settings.Source, settings.ExcludedSourceDirectories, settings.Filter);
            folders = _fileScanner.ScanFolders(settings.Destination, settings.ExcludedDestinationDirectories);
            AppendDirectoriesWithKeyBinds();
        }
        public void AppendDirectoriesWithKeyBinds()
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
            if (files[file.FIndex!.Value].Name != file.Name)
            {
                _logger.LogWarning("The file index has changed, you need to reload session");
                return;
            }
            file.PhysicalPath = _fileOperations.Move(file.PhysicalPath, destiny);
            files[file.FIndex.Value].PhysicalPath = file.PhysicalPath;
        }
        public Task<string> GetTextFileContent(File file)
        {
            if (file == null) throw new Exception("No file");
            return Task.FromResult(_fileOperations.ReadTextPreview(file.PhysicalPath));
        }
        public bool CreateFolder(string folderName)
        {
            if (folders.Any(f => f.Name == folderName)) return false;
            string? fullPath = _fileOperations.CreateFolder(settings.Destination, folderName);
            if (fullPath == null) return false;
            folders.Add(new Folder(fullPath, Path.GetFileName(fullPath)));
            return true;
        }
        public void ChangeFileName(int index, string newFileName)
        {
            File file = files[index];
            string? newFullPath = _fileOperations.Rename(file.PhysicalPath, newFileName, file.Extension);
            if (newFullPath == null) return;
            file.PhysicalPath = newFullPath;
            file.Name = newFileName;
            file.Path = urlBuilder.GetUrl(newFullPath);
        }
    }
}
