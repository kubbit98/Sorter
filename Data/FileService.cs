using Microsoft.Extensions.Options;
using System.IO;

namespace Sorter.Data
{
    public class FileService
    {
        private readonly IConfiguration _configuration;
        private readonly IOptionsMonitor<ConfigOptions> _options;
        private List<File> Files;
        private List<Folder> Folders;
        private int indexOfActualProcessingFile;

        private string source;
        private string destination;
        private string[] excludeDirs;
        private string[] whiteList;
        private string[] blackList;
        private bool UseWhiteListInsteadOfBlackList;
        //private int findex2 = 0;
        public FileService(IConfiguration configuration, IOptionsMonitor<ConfigOptions> options)
        {
            _configuration = configuration;
            _options = options;
            try
            {
                source = Path.GetFullPath(_options.CurrentValue.Source);
                destination = Path.GetFullPath(_options.CurrentValue.Destination);
                excludeDirs = Array.ConvertAll(_options.CurrentValue.ExcludeDirs, dir =>dir=Path.GetFullPath(dir));
                whiteList = _options.CurrentValue.WhiteList;
                blackList = _options.CurrentValue.BlackList;
                UseWhiteListInsteadOfBlackList = _options.CurrentValue.UseWhiteListInsteadOfBlackList;
                
            }
            catch (NullReferenceException)
            {
                throw new NullReferenceException("Probably there is error in config.json");
            }
            catch (Exception)
            {
                throw;
            }
            LoadFilesAndFolders();
            indexOfActualProcessingFile = -1;
        }
        //public Dictionary<string, string> Destinations = new Dictionary<string, string>();
        //public int FileNumber = 0;

        /*public Task<File[]> GetFilesAsync()
        {
            return Task.FromResult(Files.ToArray());
        }*/
        public Task<Folder[]> GetFoldersAsync()
        {
            return Task.FromResult(Folders.ToArray());
        }
        /*public File GetFile()
        {
            File f = Files[findex];
            f.Path = f.PhysicalPath;
            if (f.Path.Contains(source)) f.Path = f.Path.Replace(source, "/src");
            else if (f.Path.Contains(destination)) f.Path = f.Path.Replace(destination, "/dest");
            f.Path = f.Path.Replace("\\", "/");
            f.FIndex= findex;
            return f;
        }*/
        /*public File GetNextFile()
        {
            if (findex < Files.Count - 1) ++findex;
            return GetFile();
        }*/
        public int? GetNextIndex()
        {
            /*if (findex < Files.Count - 1)
            {
                //++findex;
                return ++findex;

            }
            return null;*/
            return (indexOfActualProcessingFile < Files.Count - 1) ? ++indexOfActualProcessingFile : null;
        }
        public File? GetFileAtIndex(int index)
        {
            if (index < Files.Count && index >= 0)
            {
                File file = Files[index];
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
            /*if (index < Files.Count && index >= 0)//-1?
            {
                File f = Files[index];
                if (!f.Name.Equals(name)) throw new Exception("You need to reload yor session, the server was reset");
                return PrepareFileToGet(f,index);
            }*/
            //return null;
        }
        public void ResetFiles()
        {
            try
            {
                source = Path.GetFullPath(_options.CurrentValue.Source);
                destination = Path.GetFullPath(_options.CurrentValue.Destination);
                excludeDirs = Array.ConvertAll(_options.CurrentValue.ExcludeDirs, dir => dir = Path.GetFullPath(dir));
                whiteList = _options.CurrentValue.WhiteList;
                blackList = _options.CurrentValue.BlackList;
                UseWhiteListInsteadOfBlackList = _options.CurrentValue.UseWhiteListInsteadOfBlackList;

            }
            catch (NullReferenceException)
            {
                throw new NullReferenceException("Probably there is error in config.json");
            }
            catch (Exception)
            {
                throw;
            }
            LoadFilesAndFolders();
            indexOfActualProcessingFile = -1;
        }
        /*public File GetPreviousFile()
        {
            if (findex > 0) --findex;
            return GetFile();
        }*/
        private void LoadFilesAndFolders()
        {
            try
            {
                Files = ProcessFilesInDirectoryRecursively(source);
                Folders = ProcessDirectoryRecursively(destination);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }

        }
        private List<Folder> ProcessDirectoryRecursively(string targetDirectoryPath)
        {
            List<Folder> folders = new List<Folder>();
            foreach (var fullPathToDirectory in Directory.GetDirectories(targetDirectoryPath))
            {
                try
                {
                    var folderName = Path.GetFileName(fullPathToDirectory);
                    if (!excludeDirs.Contains(fullPathToDirectory))
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
            return folders;
        }
        private List<File> ProcessFilesInDirectoryRecursively(string sourceDirectoryPath)
        {
            List<File> files = new List<File>();
            foreach (var fullPathToFile in Directory.GetFiles(sourceDirectoryPath))
            {
                /*try
                {*/
                var fileName = Path.GetFileName(fullPathToFile);
                var fileExtension = Path.GetExtension(fullPathToFile).Replace(".", "").ToLower();
                //var pathToFileSplitTab = fullPathToFile.Split(Path.DirectorySeparatorChar);
                //var fileName = pathToFileSplitTab[pathToFileSplitTab.Length - 1];
                //var fileExtension = fileName.Split('.').Last().ToLower();
                if ((UseWhiteListInsteadOfBlackList && !whiteList.Contains(fileExtension))
                    || (!UseWhiteListInsteadOfBlackList && blackList.Contains(fileExtension))) continue;
                files.Add(new File(fullPathToFile, fileName, fileExtension));
                //Console.WriteLine(findex2++);
                //}
                /*catch (Exception e)
                {
                    Console.WriteLine("Error with file " + fullPathToFile + "\n" + e.Message);
                }*/
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
            return files;
        }
        public void MoveFile(File file, string destiny)
        {
            try
            {
                //Console.WriteLine(file.OriginalPath + " " + destiny+"\\"+file.Name);
                if (Files[file.FIndex.Value].Name == file.Name)
                {
                    System.IO.File.Move(file.PhysicalPath, Path.Combine(destiny,file.Name));
                    file.PhysicalPath = Path.Combine(destiny, file.Name);
                    Files[file.FIndex.Value].PhysicalPath = file.PhysicalPath;
                }
                else
                {
                    Console.WriteLine("The file index has changed, you need to reload session");
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("Cannot move file\n" + e.Message);
            }
        }
    }
}
