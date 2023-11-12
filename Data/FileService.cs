using Microsoft.Extensions.Options;

namespace Sorter.Data
{
    public class FileService
    {
        private readonly IConfiguration _configuration;
        private readonly IOptionsMonitor<ConfigOptions> _options;
        private readonly ConfigOptionsService _configOptionsService;
        private readonly ILogger _logger;

        private static int s_maxSizeOfPhotoInPixels = 850;
        private static string[] s_thumbnailExtensions = { "bmp", "gif", "jpg", "jpeg", "pbm", "png", "tiff", "tga", "webp" };
        private static string[] s_globalExtensionExclude = { "ds_store" };
        private static int s_thumbnailsToCreateAhead = 5;
        private List<File> files = new List<File>();
        private List<Folder> folders = new List<Folder>();
        private string source = "";
        private string destination = "";
        private string[] excludeDirsInSource = Array.Empty<string>();
        private string[] excludeDirsInDestination = Array.Empty<string>();
        private string[] whiteList = Array.Empty<string>();
        private string[] blackList = Array.Empty<string>();
        private bool useWhiteListInsteadOfBlackList;
        private string password;
        private bool allowRename;
        private bool useThumbnails;
        private bool initialized;
        private string sessionStoragePrefix;
        private int indexOfLastProcessingFile = -1;



        /// <summary>
        /// Initializes a new instance of the <see cref="FileService"/> class with the specified configuration, options, configuration options service, and logger.
        /// </summary>
        /// <param name="configuration">The configuration.</param>
        /// <param name="options">The options.</param>
        /// <param name="configOptionsService">The configuration options service.</param>
        /// <param name="logger">The logger.</param>
        public FileService(IConfiguration configuration, IOptionsMonitor<ConfigOptions> options, ConfigOptionsService configOptionsService, ILogger<FileService> logger)
        {
            _configuration = configuration;
            _options = options;
            _configOptionsService = configOptionsService;
            _logger = logger;
            password = _configuration.GetValue<string>("Password");
            sessionStoragePrefix = Path.GetRandomFileName();
            initialized = false;

            // source = Path.GetFullPath(_options.CurrentValue.Source ?? "");
            // excludeDirsInSource = Array.ConvertAll(_options.CurrentValue.ExcludeDirsSource ?? Array.Empty<string>(), dir => Path.GetFullPath(dir));
            // destination = Path.GetFullPath(_options.CurrentValue.Destination ?? "");
            // excludeDirsInDestination = Array.ConvertAll(_options.CurrentValue.ExcludeDirsDestination ?? Array.Empty<string>(), dir => Path.GetFullPath(dir));
            // whiteList = _options.CurrentValue.WhiteList ?? Array.Empty<string>();
        }

        /// <summary>
        /// Checks the correctness of the configuration.
        /// </summary>
        /// <returns>True if the configuration is correct, false otherwise.</returns>
        public bool CheckCorrectnessOfConfig()
        {
            if (string.IsNullOrWhiteSpace(_options.CurrentValue.Source)) return false;
            if (string.IsNullOrWhiteSpace(_options.CurrentValue.Destination)) return false;
            if (!Directory.Exists(Path.GetFullPath(_options.CurrentValue.Source))) return false;
            if (!Directory.Exists(Path.GetFullPath(_options.CurrentValue.Destination))) return false;
            return true;
        }

        /// <summary>
        /// Initializes the files and folders for sorting.
        /// </summary>
        /// <param name="reInit">If true, re-initializes the files and folders.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains a boolean value indicating whether the initialization was successful.</returns>
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

        /// <summary>
        /// Loads the configuration settings from the options and configuration files.
        /// </summary>
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
        
        /// <summary>
        /// Gets the storage prefix.
        /// </summary>
        /// <returns>The storage prefix.</returns>
        public Task<string> GetStoragePrefix()
        {
            return Task.FromResult(sessionStoragePrefix);
        }

        /// <summary>
        /// Gets the password.
        /// </summary>
        /// <returns>The password.</returns>
        public Task<string> GetPassword()
        {
            return Task.FromResult(password);
        }

        /// <summary>
        /// Gets the value of AllowRename from the current options.
        /// </summary>
        /// <returns>A task that represents the asynchronous operation. The task result contains the value of AllowRename.</returns>
        public Task<bool> GetAllowRename()
        {
            allowRename = _options.CurrentValue.AllowRename;
            return Task.FromResult(allowRename);
        }

        /// <summary>
        /// Returns an array of folders asynchronously.
        /// </summary>
        /// <returns>A task that represents the asynchronous operation. The task result contains an array of folders.</returns>
        public Task<Folder[]> GetFoldersAsync()
        {
            return Task.FromResult(folders.ToArray());
        }

        /// <summary>
        /// Represents a file in the file system.
        /// </summary>
        private File GetCopyOfFile(File file)
        {
            return new File(file.PhysicalPath, file.Name, file.Extension) { ThumbnailPath = file.ThumbnailPath };
        }
        /// <summary>
        /// Gets the index of the next file to be processed.
        /// </summary>
        /// <returns>The index of the next file to be processed, or null if there are no more files to process.</returns>
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

        /// <summary>
        /// Gets the file at the specified index from the list of files.
        /// </summary>
        /// <param name="index">The index of the file to get.</param>
        /// <returns>The file at the specified index, or null if the index is out of range.</returns>
        public File? GetFileAtIndex(int index)
        {
            if (index < files.Count && index >= 0)
            {
                File file = GetCopyOfFile(files[index]);
                file.Path = file.PhysicalPath;
                if (file.Path.Contains(source)) file.Path = string.Concat("/src/", Path.GetRelativePath(source, file.PhysicalPath));
                else if (file.Path.Contains(destination)) file.Path = string.Concat("/dest/", Path.GetRelativePath(destination, file.PhysicalPath));
                file.FIndex = index;
                return file;
            }
            return null;
        }
        
        /// <summary>
        /// Gets the file at the specified index and verifies that it has the specified name.
        /// </summary>
        /// <param name="index">The index of the file to retrieve.</param>
        /// <param name="name">The expected name of the file.</param>
        /// <returns>The file at the specified index.</returns>
        public File? GetFileAtIndex(int index, string name)
        {
            File? file = GetFileAtIndex(index);
            if (file == null) return null;
            if (!file.Name.Equals(name)) throw new Exception("You need to reload yor session, the server was reset");
            return file;
        }

        /// <summary>
        /// Loads files and folders from the source and destination directories recursively.
        /// </summary>
        private void LoadFilesAndFolders()
        {
            files = ProcessFilesInDirectoryRecursively(source);
            folders = ProcessDirectoryRecursively(destination);
        }


        /// <summary>
        /// Recursively processes a directory and returns a list of all the folders found.
        /// </summary>
        /// <param name="targetDirectoryPath">The path of the directory to process.</param>
        /// <returns>A list of all the folders found in the directory.</returns>
        private List<Folder> ProcessDirectoryRecursively(string targetDirectoryPath)
        {
            List<Folder> folders = new List<Folder>();
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

        /// <summary>
        /// Recursively processes all files in the specified directory and returns a list of File objects.
        /// </summary>
        /// <param name="sourceDirectoryPath">The path of the directory to process.</param>
        /// <returns>A list of File objects representing all files in the specified directory and its subdirectories.</returns>
        private List<File> ProcessFilesInDirectoryRecursively(string sourceDirectoryPath)
        {
            List<File> files = new List<File>();
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

        /// <summary>
        /// Moves the specified file to the specified destination.
        /// </summary>
        /// <param name="file">The file to be moved.</param>
        /// <param name="destiny">The destination directory.</param>
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

        /// <summary>
        /// Gets the content of a text file.
        /// </summary>
        /// <param name="file">The file to read.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains the content of the file.</returns>
        public Task<string> GetTextFileContent(File file)
        {
            if (file == null) throw new Exception("No file"); ;
            return Task.FromResult(System.IO.File.ReadAllText(file!.PhysicalPath));
        }

        /// <summary>
        /// Creates a new folder with the given name in the destination directory.
        /// </summary>
        /// <param name="folderName">The name of the folder to create.</param>
        /// <returns>True if the folder was created successfully, false otherwise.</returns>
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

        /// <summary>
        /// Renames the file at the specified index with the new file name.
        /// </summary>
        /// <param name="index">The index of the file to rename.</param>
        /// <param name="newFileName">The new file name to use.</param>
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

        /// <summary>
        /// Creates a thumbnail for the given file if it doesn't already exist.
        /// </summary>
        /// <param name="file">The file to create a thumbnail for.</param>
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
                using (Image image = Image.Load(file.PhysicalPath))
                {
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
            }
            //Console.WriteLine("Elapsed time {0} ms", stopWatch.ElapsedMilliseconds);
        }
    }
}
