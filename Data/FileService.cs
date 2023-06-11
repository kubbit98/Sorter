using Microsoft.Extensions.Options;

namespace Sorter.Data
{
    public class FileService
    {
        private readonly IConfiguration _configuration;
        private readonly IOptionsMonitor<ConfigOptions> _options;
        private static int MAX_SIZE_OF_PHOTO = 850;
        private static string[] THUMBNAIL_EXTENSIONS = { "bmp", "gif", "jpg", "jpeg", "pbm", "png", "tiff", "tga", "webp" };

        private List<File> Files;
        private List<Folder> Folders;
        private int indexOfActualProcessingFile;

        private string source;
        private string destination;
        private string[] excludeDirsInSource;
        private string[] excludeDirsInDestination;
        private string[] whiteList;
        private string[] blackList;
        private bool useWhiteListInsteadOfBlackList;
        private string truePassword;
        private bool allowRename;
        private bool useThumbnails;

        public FileService(IConfiguration configuration, IOptionsMonitor<ConfigOptions> options)
        {
            _configuration = configuration;
            _options = options;
            LoadConfig();
            LoadFilesAndFolders();
            indexOfActualProcessingFile = -1;
            if (useThumbnails && Files!.Count > 0)
            {
                foreach (var f in Files.GetRange(0, 5))
                {
                    Task.Run(() => CreateThumbnail(f));
                }
            }
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
                truePassword = _options.CurrentValue.Password;
                allowRename = _options.CurrentValue.AllowRename;
                useThumbnails = _options.CurrentValue.UseThumbnails;

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
        public Task<string> GetTruePassword()
        {
            truePassword = _options.CurrentValue.Password;
            return Task.FromResult(truePassword);
        }
        public Task<bool> GetAllowRename()
        {
            allowRename = _options.CurrentValue.AllowRename;
            return Task.FromResult(allowRename);
        }
        public Task<Folder[]> GetFoldersAsync()
        {
            return Task.FromResult(Folders.ToArray());
        }
        private File GetCopyOfFile(File file)
        {
            return new File(file.PhysicalPath, file.Name, file.Extension) { ThumbnailPath = file.ThumbnailPath };
        }
        public int? GetNextIndex()
        {
            if (indexOfActualProcessingFile < Files.Count - 1)
            {
                indexOfActualProcessingFile++;
                if (useThumbnails && indexOfActualProcessingFile + 5 < Files.Count)
                {
                    Task.Run(() => CreateThumbnail(Files[indexOfActualProcessingFile + 5]));
                }
                return indexOfActualProcessingFile;
            }

            return null;//(indexOfActualProcessingFile < Files.Count - 1) ? ++indexOfActualProcessingFile : null;
        }
        public File? GetFileAtIndex(int index)
        {
            if (index < Files.Count && index >= 0)
            {
                File file = GetCopyOfFile(Files[index]);
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
        public void ResetFiles()
        {
            LoadConfig();
            LoadFilesAndFolders();
            indexOfActualProcessingFile = -1;
            if (useThumbnails && Files.Count > 0)
            {
                foreach (var f in Files.GetRange(0, 5))
                {
                    Task.Run(() => CreateThumbnail(f));
                }
            }
        }
        private void LoadFilesAndFolders()
        {
            Files = ProcessFilesInDirectoryRecursively(source);
            Folders = ProcessDirectoryRecursively(destination);
        }
        private List<Folder> ProcessDirectoryRecursively(string targetDirectoryPath)
        {
            List<Folder> folders = new List<Folder>();
            try
            {
                if (!Directory.Exists(Path.GetFullPath(targetDirectoryPath)))
                {
                    return folders;
                }
                foreach (var fullPathToDirectory in Directory.GetDirectories(targetDirectoryPath))
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
                        Console.WriteLine("Error with folder " + fullPathToDirectory + "\n" + e.Message);
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("Error with whole destination folder " + e.Message);
            }
            return folders;
        }
        private List<File> ProcessFilesInDirectoryRecursively(string sourceDirectoryPath)
        {
            List<File> files = new List<File>();
            try
            {
                if (!Directory.Exists(Path.GetFullPath(sourceDirectoryPath)) || excludeDirsInSource.Contains(Path.GetFullPath(sourceDirectoryPath)))
                {
                    return files;
                }
                foreach (var fullPathToFile in Directory.GetFiles(sourceDirectoryPath))
                {
                    var fileName = Path.GetFileNameWithoutExtension(fullPathToFile);
                    var fileExtension = Path.GetExtension(fullPathToFile).Replace(".", "").ToLower();
                    if ((useWhiteListInsteadOfBlackList && !whiteList.Contains(fileExtension))
                        || (!useWhiteListInsteadOfBlackList && blackList.Contains(fileExtension))) continue;
                    files.Add(new File(fullPathToFile, fileName, fileExtension));
                }
                foreach (var fullPath in Directory.GetDirectories(sourceDirectoryPath))
                {
                    try
                    {
                        files.AddRange(ProcessFilesInDirectoryRecursively(fullPath));
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine("Error with folder " + fullPath + "\n" + e.Message);
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("Error with whole source folder " + e.Message);
            }
            return files;
        }
        public void MoveFile(File file, string destiny)
        {
            try
            {
                if (Files[file!.FIndex!.Value].Name == file.Name)
                {
                    System.IO.File.Move(file.PhysicalPath, Path.Combine(destiny, file.Name));
                    file.PhysicalPath = Path.Combine(destiny, file.Name);
                    Files[file.FIndex.Value].PhysicalPath = file.PhysicalPath;
                    Console.WriteLine("File " + file.Name + " moved to " + destiny);
                }
                else Console.WriteLine("The file index has changed, you need to reload session");
            }
            catch (Exception e)
            {
                Console.WriteLine("Cannot move file\n" + e.Message);
            }
        }
        public Task<string> GetTextFileContent(File file)
        {
            if (file == null) throw new Exception("No file"); ;
            return Task.FromResult(System.IO.File.ReadAllText(file!.PhysicalPath));
        }
        public bool CreateFolder(string folderName)
        {
            if (Folders.Any(f => f.Name == folderName)) return false;
            string fullPath = Path.Combine(destination, folderName);
            Directory.CreateDirectory(fullPath);
            folderName = Path.GetFileName(fullPath);
            if (folderName != null)
            {
                Folders.Add(new Folder(fullPath, folderName));
                return true;
            }
            return false;
        }
        public void ChangeFileName(int index, string newFileName)
        {
            File file = Files[index];
            string newFullPath = Path.Combine(Directory.GetParent(file.PhysicalPath)!.FullName, string.Concat(newFileName, ".", file.Extension));
            Console.WriteLine("File renamed from " + file.Name + "." + file.Extension + " to " + newFileName + "." + file.Extension);
            System.IO.File.Move(file.PhysicalPath, newFullPath);
            Files[index].PhysicalPath = newFullPath;
            Files[index].Name = newFileName;
            Files[index].Path = newFullPath;
            if (Files[index].Path.Contains(source)) Files[index].Path = string.Concat("/src/", Path.GetRelativePath(source, Files[index].PhysicalPath));
            else if (Files[index].Path.Contains(destination)) Files[index].Path = string.Concat("/dest/", Path.GetRelativePath(destination, Files[index].PhysicalPath));
        }
        public void CreateThumbnail(File file)
        {
            if (!THUMBNAIL_EXTENSIONS.Contains(file.Extension) || !string.IsNullOrWhiteSpace(file.ThumbnailPath))
                return;
            //System.Diagnostics.Stopwatch stopWatch = System.Diagnostics.Stopwatch.StartNew();
            using (Image image = Image.Load(file.PhysicalPath))
            {
                do
                {
                    file.ThumbnailPath = Path.Combine(Path.GetFullPath(_configuration.GetValue<string>("TempPath")), Path.ChangeExtension(Path.GetRandomFileName(), file.Extension));
                } while (System.IO.File.Exists(file.ThumbnailPath));
                double ratioWidth = MAX_SIZE_OF_PHOTO / (double)image.Width;
                double ratioHeight = MAX_SIZE_OF_PHOTO / (double)image.Height;
                double ratio = Math.Min(ratioWidth, ratioHeight);
                if (ratio < 1)
                {
                    image.Mutate(x => x.Resize((int)(image.Width * ratio), (int)(image.Height * ratio)));
                    image.Save(file.ThumbnailPath);
                    file.ThumbnailPath = string.Concat("/tmp/", Path.GetRelativePath(_configuration.GetValue<string>("TempPath"), file.ThumbnailPath));
                }
                else
                {
                    file.ThumbnailPath = string.Empty;
                }
            }
            //Console.WriteLine("Elapsed time {0} ms", stopWatch.ElapsedMilliseconds);
        }
    }
}
