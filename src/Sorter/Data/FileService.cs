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
        private readonly IThumbnailGenerator _thumbnailGenerator;
        private readonly FileScanner _fileScanner;

        private static readonly int s_thumbnailsToCreateAhead = 5;
        private static readonly int s_maxLengthOfTextFile = 5000;

        private List<File> files = [];
        private List<Folder> folders = [];
        private int indexOfLastProcessingFile = -1;
        private string sessionStoragePrefix;

        private SorterSettings settings = SorterSettings.Empty;
        private MediaUrlBuilder urlBuilder = MediaUrlBuilder.Empty;
        private readonly string password;
        private bool initialized;

        public FileService(IConfiguration configuration, IOptionsMonitor<ConfigOptions> options, IOptionsMonitor<KeyBindsOptions> keyOptions, ConfigOptionsService configOptionsService, ILogger<FileService> logger, IThumbnailGenerator thumbnailGenerator, FileScanner fileScanner)
        {
            _fileScanner = fileScanner;
            _thumbnailGenerator = thumbnailGenerator;
            _configuration = configuration;
            _options = options;
            _keyOptions = keyOptions;
            _configOptionsService = configOptionsService;
            _logger = logger;
            password = _configuration.GetValue<string>("Password") ?? string.Empty;
            sessionStoragePrefix = Path.GetRandomFileName();
            initialized = false;
        }
        public Task<bool> InitializeFiles(bool reInit)
        {
            if (!initialized || reInit)
            {
                while (!_configOptionsService.CheckMonitor(_options.CurrentValue)) Thread.Sleep(100);
                if (!SorterSettings.TryCreate(_options.CurrentValue, _configuration, out var newSettings)) return Task.FromResult(false);
                settings = newSettings;
                urlBuilder = new MediaUrlBuilder(settings.Source, settings.Destination);
                LoadFilesAndFolders();
                indexOfLastProcessingFile = -1;
                if (settings.UseThumbnails && files.Count > 0)
                {
                    foreach (var f in files.GetRange(0, Math.Min(files.Count, s_thumbnailsToCreateAhead)))
                    {
                        Task.Run(() => CreateThumbnail(f));
                    }
                    while (files[0].IsThumbnailExist == File.ThumbnailEnum.Unknown || files[0].IsThumbnailExist == File.ThumbnailEnum.WillBe) Thread.Sleep(100);
                }
                initialized = true;
                if (reInit) sessionStoragePrefix = Path.GetRandomFileName();
            }
            return Task.FromResult(true);
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
                if (settings.UseThumbnails)
                {
                    foreach (var f in files.GetRange(indexOfLastProcessingFile, Math.Min(files.Count - indexOfLastProcessingFile, s_thumbnailsToCreateAhead)))
                    {
                        if (f.IsThumbnailExist == File.ThumbnailEnum.Unknown) Task.Run(() => CreateThumbnail(f));
                    }
                }
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
            return Task.FromResult(!System.IO.File.Exists(Path.Combine(destiny, file.NameWithExtension)));
        }
        public void MoveFile(File file, string destiny)
        {
            string newFullPath = Path.Combine(destiny, file.NameWithExtension);
            try
            {
                if (files[file!.FIndex!.Value].Name == file.Name)
                {
                    System.IO.File.Move(file.PhysicalPath, newFullPath);
                    file.PhysicalPath = newFullPath;
                    files[file.FIndex.Value].PhysicalPath = file.PhysicalPath;
                    _logger.LogInformation("File {FileNameExt} moved to {Destination}", file.NameWithExtension, destiny);
                }
                else _logger.LogWarning("The file index has changed, you need to reload session");
            }
            catch (Exception e)
            {
                _logger.LogError("File {FileNameExt} cannot be moved to {destiny}\n{Error}", file.NameWithExtension, destiny, e.Message);
                throw;
            }
        }
        public Task<string> GetTextFileContent(File file)
        {
            if (file == null) throw new Exception("No file");
            string content = System.IO.File.ReadAllText(file.PhysicalPath);
            if (content.Length > s_maxLengthOfTextFile)
            {
                content = string.Concat(content[..s_maxLengthOfTextFile], "[...]");
            }
            return Task.FromResult(content);
        }
        public bool CreateFolder(string folderName)
        {
            if (folders.Any(f => f.Name == folderName)) return false;
            if (folderName.IndexOfAny([.. Path.GetInvalidFileNameChars()]) != -1) return false;
            string fullPath = Path.Combine(settings.Destination, folderName);
            Directory.CreateDirectory(fullPath);
            folderName = Path.GetFileName(fullPath);
            if (folderName != null)
            {
                folders.Add(new Folder(fullPath, folderName));
                return true;
            }
            return false;
        }
        public void ChangeFileName(int index, string newFileName)
        {
            if (newFileName.IndexOfAny([.. Path.GetInvalidFileNameChars()]) != -1) return;
            File file = files[index];
            string newFullPath = Path.Combine(Directory.GetParent(file.PhysicalPath)!.FullName, string.Concat(newFileName, ".", file.Extension));
            if (System.IO.File.Exists(newFullPath)) throw new InvalidOperationException("File with that name exists");
            try
            {
                System.IO.File.Move(file.PhysicalPath, newFullPath);
                _logger.LogInformation("File renamed from {FileNameExt} to {Name}.{Extension}", file.NameWithExtension, newFileName, file.Extension);
                files[index].PhysicalPath = newFullPath;
                files[index].Name = newFileName;
                files[index].Path = urlBuilder.GetUrl(newFullPath);
            }
            catch (Exception e)
            {
                _logger.LogError("File {FileNameExt} cannot be renamed to {NewFileName}\n{Error}", file.NameWithExtension, newFileName, e.Message);
                throw;
            }
        }
        public void CreateThumbnail(File file)
        {
            if (file.IsThumbnailExist != File.ThumbnailEnum.Unknown) return;
            if (!_thumbnailGenerator.CanProcess(file.Extension))
            {
                file.IsThumbnailExist = File.ThumbnailEnum.WillNot;
                return;
            }
            file.IsThumbnailExist = File.ThumbnailEnum.WillBe;
            try
            {
                ThumbnailResult result = _thumbnailGenerator.Generate(file.PhysicalPath, file.Extension);
                if (result.ConvertedPath != null) file.ConvertedPath = MediaUrlBuilder.GetTempUrl(result.ConvertedPath);
                if (result.ThumbnailPath != null) file.ThumbnailPath = MediaUrlBuilder.GetTempUrl(result.ThumbnailPath);
                file.IsThumbnailExist = result.ThumbnailPath != null ? File.ThumbnailEnum.Exists : File.ThumbnailEnum.WillNot;
            }
            catch (InvalidImageFileException e)
            {
                _logger.LogWarning("Thumbnail cannot be created for {FilePath}: {Reason}", file.PhysicalPath, e.Message);
                file.IsThumbnailExist = File.ThumbnailEnum.WillNot;
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Thumbnail cannot be created for {FilePath}", file.PhysicalPath);
                file.IsThumbnailExist = File.ThumbnailEnum.WillNot;
            }
        }
    }
}
