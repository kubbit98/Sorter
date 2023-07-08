using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Primitives;

namespace Sorter.Data
{
    public class SourceDFP : DynamicFileProvider
    {
        public SourceDFP(string root) : base(root)
        {
        }
    }
    public class DestinationDFP : DynamicFileProvider
    {
        public DestinationDFP(string root) : base(root)
        {
        }
    }
    public class TempDFP : DynamicFileProvider
    {
        public TempDFP(string root) : base(root)
        {
            if (string.IsNullOrWhiteSpace(root))
            {
                root = "Temp";
            }
            string path = Path.GetFullPath(root);
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
                Console.WriteLine("Creating folder for thumbnails in " + path);
            }
            else
            {
                DirectoryInfo di = new DirectoryInfo(path);
                foreach (FileInfo file in di.EnumerateFiles())
                {
                    file.Delete();
                }
                foreach (DirectoryInfo dir in di.EnumerateDirectories())
                {
                    dir.Delete(true);
                }
                Console.WriteLine("Clearing temporary directory at " + path);
            }
            UpdateProvider(path);
        }
    }
    public class DynamicFileProvider : IFileProvider
    {
        PhysicalFileProvider PhysicalFileProvider { get; set; }
        public DynamicFileProvider(string root)
        {
            if (!string.IsNullOrWhiteSpace(root) && Directory.Exists(Path.GetFullPath(root))) PhysicalFileProvider = new PhysicalFileProvider(Path.GetFullPath(root));
        }

        public IDirectoryContents GetDirectoryContents(string subpath)
        {
            return ((IFileProvider)PhysicalFileProvider).GetDirectoryContents(subpath);
        }

        public IFileInfo GetFileInfo(string subpath)
        {
            try
            {
                return ((IFileProvider)PhysicalFileProvider).GetFileInfo(subpath);
            }
            catch
            {
                Console.WriteLine("Exception in PhysicalFileProvider. 99% of the time it's a wrong configuration. All you have to do is set the configuration accordingly and initialize the service.");
                return new NotFoundFileInfo("heheheheh");
            }
        }

        public IChangeToken Watch(string filter)
        {
            return ((IFileProvider)PhysicalFileProvider).Watch(filter);
        }
        public void UpdateProvider(string newPath)
        {
            PhysicalFileProvider = new PhysicalFileProvider(newPath);
        }
    }
}
