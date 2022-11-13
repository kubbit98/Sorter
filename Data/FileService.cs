using System.Security.Cryptography;
using System.Xml.Linq;

namespace Sortownik.Data;

public class FileService
{
    private readonly IConfiguration _configuration;
    private List<File> Files;
    private List<Folder> Folders;
    private int findex;

    private string source;
    private string destination;
    private string[] excludeDirs;
    private string[] whiteList;
    private string[] blackList;
    private bool UseWhiteListInsteadOfBlackList;
    //private int findex2 = 0;
    //MD5 md5;
    public FileService(IConfiguration configuration)
    {
        _configuration = configuration;
        try
        {
            source = _configuration["Source"];
            destination = _configuration["Destination"];
            excludeDirs = _configuration.GetSection("ExcludeDirs").Get<string[]>();
            whiteList = _configuration.GetSection("WhiteList").Get<string[]>();
            blackList = _configuration.GetSection("BlackList").Get<string[]>();
            UseWhiteListInsteadOfBlackList = Convert.ToBoolean(_configuration["UseWhiteListInsteadOfBlackList"]);
            /*string test = _configuration["Destination2"];
            test.Split();*/
        }
        catch (NullReferenceException)
        {
            throw new NullReferenceException("Probably there is error in configuration.json");
        }
        catch (Exception)
        {
            throw;
        }
        //md5 = MD5.Create();
        LoadFilesAndFolders();
        findex = -1;
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
        if (findex < Files.Count - 1)
        {
            ++findex;
            return findex;

        }
        return null;
    }
    public File? GetFileAtIndex(int index)
    {
        if (index < Files.Count && index>=0)
        {
            File f = Files[index];
            f.Path = f.PhysicalPath;
            if (f.Path.Contains(source)) f.Path = f.Path.Replace(source, "/src");
            else if (f.Path.Contains(destination)) f.Path = f.Path.Replace(destination, "/dest");
            f.Path = f.Path.Replace("\\", "/");
            f.FIndex = index;
            return f;
        }
        return null;
    }
    public File? GetFileAtIndex(int index,string name)
    {
        if (index < Files.Count - 1 && index >= 0)
        {
            File f = Files[index];
            if (!f.Name.Equals(name)) throw new Exception("You need to reload yor session, the server was reset");
            f.Path = f.PhysicalPath;
            if (f.Path.Contains(source)) f.Path = f.Path.Replace(source, "/src");
            else if (f.Path.Contains(destination)) f.Path = f.Path.Replace(destination, "/dest");
            f.Path = f.Path.Replace("\\", "/");
            f.FIndex = index;
            return f;
        }
        return null;
    }
    public void ResetFiles()
    {
        LoadFilesAndFolders();
        findex = -1;
    }
    /*public File GetPreviousFile()
    {
        if (findex > 0) --findex;
        return GetFile();
    }*/
    public void LoadFilesAndFolders()
    {
        Files = new List<File>();
        Folders = new List<Folder>();
        ProcessFilesInDirectory(source);
        ProcessDirectory(destination);
    }
    public void ProcessDirectory(string targetDirectory)
    {
        string[] folderEntries = Directory.GetDirectories(targetDirectory);
        foreach (var fullPath in folderEntries)
        {
            try
            {
                var tab = fullPath.Split('\\');
                var name = tab[tab.Length - 1];
                //Console.WriteLine(fullPath + " " + name );
                if (!excludeDirs.Contains(fullPath))
                {
                    Folders.Add(new Folder() { Path = fullPath, Name = name });
                    ProcessDirectory(fullPath);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("Error with folder " + fullPath);
                Console.WriteLine(e.Message);
            }
        }
    }
    public void ProcessFilesInDirectory(string sourceDirectory)
    {
        string[] fileEntries = Directory.GetFiles(sourceDirectory);
        string[] folderEntries = Directory.GetDirectories(sourceDirectory);
        foreach (var fullPath in fileEntries)
        {
            try
            {
                var tab = fullPath.Split('\\');
                var name = tab[tab.Length - 1];
                var extension = name.Split('.').Last().ToLower();
                if (UseWhiteListInsteadOfBlackList)
                {
                    if (!whiteList.Contains(extension)) continue;
                }
                else
                {
                    if(blackList.Contains(extension)) continue;
                }
                //FileStream filestream = new FileStream(fullPath, FileMode.Open);
                //filestream.Position = 0;
                //MD5=md5.ComputeHash(filestream)
                Files.Add(new File() { Path = fullPath, Extension = extension, Name = name, PhysicalPath = fullPath });
                //Console.WriteLine(findex2++);
            }
            catch (Exception e)
            {
                Console.WriteLine("Error with file " + fullPath);
                Console.WriteLine(e.Message);
            }
        }
        foreach (var fullPath in folderEntries)
        {
            try
            {
                ProcessFilesInDirectory(fullPath);
            }
            catch (Exception e)
            {
                Console.WriteLine("Error with folder " + fullPath);
                Console.WriteLine(e.Message);
            }
        }
    }
    public void MoveFile(File file, string destiny)
    {
        try
        {
            //Console.WriteLine(file.OriginalPath + " " + destiny+"\\"+file.Name);
            if (Files[file.FIndex.Value].Name == file.Name)
            {
                System.IO.File.Move(file.PhysicalPath, destiny+"\\"+file.Name);
                file.PhysicalPath = destiny + "\\" + file.Name;
                Files[file.FIndex.Value] = file;
            }
            else
            {
                Console.WriteLine("The file index has changed, you need to reload browser");
            }
        }
        catch (Exception e)
        {
            Console.WriteLine("Cannot move file\n" + e.Message);
        }
    }
}
