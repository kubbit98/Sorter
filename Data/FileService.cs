﻿using Microsoft.Extensions.Options;

namespace Sorter.Data
{
    public class FileService
    {
        private readonly IConfiguration _configuration;
        private readonly IOptionsMonitor<ConfigOptions> _options;
        private readonly IOptionsMonitor<KeyBindsOptions> _keyOptions;
        private readonly ConfigOptionsService _configOptionsService;
        private readonly ILogger _logger;

        private static readonly int s_maxSizeOfPhotoInPixels = 850;
        private static readonly string[] s_thumbnailExtensions = { "bmp", "gif", "jpg", "jpeg", "pbm", "png", "tiff", "tga", "webp" };
        private static readonly string[] s_globalExtensionExclude = { "ds_store" };
        private static readonly int s_thumbnailsToCreateAhead = 5;
        private static readonly int s_maxLengthOfTextFile = 5000;

        private List<File> files = new();
        private List<Folder> folders = new();
        private int indexOfLastProcessingFile = -1;
        private string sessionStoragePrefix;

        private string source = "";
        private string destination = "";
        private string[] excludeDirsInSource = Array.Empty<string>();
        private string[] excludeDirsInDestination = Array.Empty<string>();
        private string[] whiteList = Array.Empty<string>();
        private string[] blackList = Array.Empty<string>();
        private bool useWhiteListInsteadOfBlackList;
        private readonly string password;
        private bool allowRename;
        private bool useThumbnails;
        private bool initialized;

        public FileService(IConfiguration configuration, IOptionsMonitor<ConfigOptions> options, IOptionsMonitor<KeyBindsOptions> keyOptions, ConfigOptionsService configOptionsService, ILogger<FileService> logger)
        {
            _configuration = configuration;
            _options = options;
            _keyOptions = keyOptions;
            _configOptionsService = configOptionsService;
            _logger = logger;
            password = _configuration.GetValue<string>("Password");
            sessionStoragePrefix = Path.GetRandomFileName();
            initialized = false;
        }
        public bool CheckCorrectnessOfConfig()
        {
            if (string.IsNullOrWhiteSpace(_options.CurrentValue.Source)) return false;
            if (string.IsNullOrWhiteSpace(_options.CurrentValue.Destination)) return false;
            if (!Directory.Exists(Path.GetFullPath(_options.CurrentValue.Source))) return false;
            if (!Directory.Exists(Path.GetFullPath(_options.CurrentValue.Destination))) return false;
            return true;
        }
        public Task<bool> InitializeFiles(bool reInit)
        {
            if (!initialized || reInit)
            {
                while (!_configOptionsService.CheckMonitor(_options.CurrentValue)) Thread.Sleep(100);
                if (!CheckCorrectnessOfConfig()) return Task.FromResult(false);
                LoadConfig();
                LoadFilesAndFolders();
                indexOfLastProcessingFile = -1;
                if (useThumbnails && files!.Count > 0)
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
        public void LoadConfig()
        {
            try
            {
                source = Path.GetFullPath(_options.CurrentValue.Source);
                excludeDirsInSource = Array.ConvertAll(_options.CurrentValue.ExcludeDirsSource, dir => dir = Path.GetFullPath(dir));
                destination = Path.GetFullPath(_options.CurrentValue.Destination);
                excludeDirsInDestination = Array.ConvertAll(_options.CurrentValue.ExcludeDirsDestination, dir => dir = Path.GetFullPath(dir));
                whiteList = _options.CurrentValue.WhiteList;
                blackList = _options.CurrentValue.BlackList;
                useWhiteListInsteadOfBlackList = _options.CurrentValue.UseWhiteListInsteadOfBlackList;
                allowRename = _options.CurrentValue.AllowRename;
                useThumbnails = _configuration.GetValue<bool>("UseThumbnails");
            }
            catch (NullReferenceException)
            {
                throw new NullReferenceException("Probably there is error in config.json");
            }
            catch (Exception)
            {
                throw;
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
            allowRename = _options.CurrentValue.AllowRename;
            return Task.FromResult(allowRename);
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
                if (useThumbnails)
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
                file.Path = file.PhysicalPath;
                if (file.Path.Contains(source)) file.Path = string.Concat("/src/", Path.GetRelativePath(source, file.PhysicalPath));
                else if (file.Path.Contains(destination)) file.Path = string.Concat("/dest/", Path.GetRelativePath(destination, file.PhysicalPath));
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
            files = ProcessFilesInDirectoryRecursively(source);
            folders = ProcessDirectoryRecursively(destination);
            AppendDirectoriesWithKeyBinds();
        }
        private List<Folder> ProcessDirectoryRecursively(string targetDirectoryPath)
        {
            List<Folder> folders = new();
            try
            {
                if (!Directory.Exists(Path.GetFullPath(targetDirectoryPath)))
                {
                    return folders;
                }
                foreach (var fullPathToDirectory in Directory.GetDirectories(targetDirectoryPath).OrderBy(f => f))
                {
                    try
                    {
                        var folderName = Path.GetFileName(fullPathToDirectory);
                        if (!excludeDirsInDestination.Contains(fullPathToDirectory))
                        {
                            folders.Add(new Folder(fullPathToDirectory, folderName));
                            folders.AddRange(ProcessDirectoryRecursively(fullPathToDirectory));
                        }
                    }
                    catch (Exception e)
                    {
                        _logger.LogError("Error with folder " + fullPathToDirectory + "\n" + e.Message);
                    }
                }
            }
            catch (Exception e)
            {
                _logger.LogError("Error with whole destination folder " + e.Message);
            }
            return folders;
        }
        private List<File> ProcessFilesInDirectoryRecursively(string sourceDirectoryPath)
        {
            List<File> files = new();
            try
            {
                if (!Directory.Exists(Path.GetFullPath(sourceDirectoryPath)) || excludeDirsInSource.Contains(Path.GetFullPath(sourceDirectoryPath)))
                {
                    return files;
                }
                foreach (var fullPathToFile in Directory.GetFiles(sourceDirectoryPath).OrderBy(f => f))
                {
                    var fileName = Path.GetFileNameWithoutExtension(fullPathToFile);
                    var fileExtension = Path.GetExtension(fullPathToFile).Replace(".", "").ToLower();
                    if (s_globalExtensionExclude.Contains(fileExtension)) continue;
                    if ((useWhiteListInsteadOfBlackList && !whiteList.Contains(fileExtension))
                        || (!useWhiteListInsteadOfBlackList && blackList.Contains(fileExtension))) continue;
                    files.Add(new File(fullPathToFile, fileName, fileExtension));
                }
                foreach (var fullPath in Directory.GetDirectories(sourceDirectoryPath).OrderBy(f => f))
                {
                    try
                    {
                        files.AddRange(ProcessFilesInDirectoryRecursively(fullPath));
                    }
                    catch (Exception e)
                    {
                        _logger.LogError("Error with folder " + fullPath + "\n" + e.Message);
                    }
                }
            }
            catch (Exception e)
            {
                _logger.LogError("Error with whole source folder " + e.Message);
            }
            return files;
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
        public void MoveFile(File file, string destiny)
        {
            try
            {
                if (files[file!.FIndex!.Value].Name == file.Name)
                {
                    System.IO.File.Move(file.PhysicalPath, Path.Combine(destiny, file.NameWithExtension));
                    file.PhysicalPath = Path.Combine(destiny, file.NameWithExtension);
                    files[file.FIndex.Value].PhysicalPath = file.PhysicalPath;
                    _logger.LogInformation("File {0} moved to {1}", file.NameWithExtension, destiny);
                }
                else _logger.LogWarning("The file index has changed, you need to reload session");
            }
            catch (Exception e)
            {
                _logger.LogError("Cannot move file\n" + e.Message);
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
            if (folderName.IndexOfAny(Path.GetInvalidFileNameChars().ToArray()) != -1) return false;
            string fullPath = Path.Combine(destination, folderName);
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
            if (newFileName.IndexOfAny(Path.GetInvalidFileNameChars().ToArray()) != -1) return;
            File file = files[index];
            string newFullPath = Path.Combine(Directory.GetParent(file.PhysicalPath)!.FullName, string.Concat(newFileName, ".", file.Extension));
            _logger.LogInformation("File renamed from " + file.Name + "." + file.Extension + " to " + newFileName + "." + file.Extension);
            System.IO.File.Move(file.PhysicalPath, newFullPath);
            files[index].PhysicalPath = newFullPath;
            files[index].Name = newFileName;
            files[index].Path = newFullPath;
            if (files[index].Path.Contains(source)) files[index].Path = string.Concat("/src/", Path.GetRelativePath(source, files[index].PhysicalPath));
            else if (files[index].Path.Contains(destination)) files[index].Path = string.Concat("/dest/", Path.GetRelativePath(destination, files[index].PhysicalPath));
        }
        public void CreateThumbnail(File file)
        {
            //System.Diagnostics.Stopwatch stopWatch = System.Diagnostics.Stopwatch.StartNew();
            if (file.IsThumbnailExist == File.ThumbnailEnum.Unknown)
            {
                if (!s_thumbnailExtensions.Contains(file.Extension))
                {
                    file.IsThumbnailExist = File.ThumbnailEnum.WillNot;
                    return;
                }
                file.IsThumbnailExist = File.ThumbnailEnum.WillBe;
                using Image image = Image.Load(file.PhysicalPath);
                double ratioWidth = s_maxSizeOfPhotoInPixels / (double)image.Width;
                double ratioHeight = s_maxSizeOfPhotoInPixels / (double)image.Height;
                double ratio = Math.Min(ratioWidth, ratioHeight);
                if (ratio < 1)
                {
                    string temporaryThumbnailPath; //im doing that like this, bec when scrolling very fast, index can get a file with an incompletely processed thumbnail
                    do
                    {
                        temporaryThumbnailPath = Path.Combine(Path.GetFullPath(_configuration.GetValue<string>("TempPath")), Path.ChangeExtension(Path.GetRandomFileName(), file.Extension));
                    } while (System.IO.File.Exists(temporaryThumbnailPath));
                    image.Mutate(x => x.Resize((int)(image.Width * ratio), (int)(image.Height * ratio)));
                    do
                    {
                        image.Save(temporaryThumbnailPath);
                    } while (!System.IO.File.Exists(temporaryThumbnailPath));
                    file.ThumbnailPath = string.Concat("/tmp/", Path.GetRelativePath(_configuration.GetValue<string>("TempPath"), temporaryThumbnailPath));
                    file.IsThumbnailExist = File.ThumbnailEnum.Exists;
                }
                else
                {
                    file.IsThumbnailExist = File.ThumbnailEnum.WillNot;
                }
            }
            //Console.WriteLine("Elapsed time {0} ms", stopWatch.ElapsedMilliseconds);
        }
    }
}
