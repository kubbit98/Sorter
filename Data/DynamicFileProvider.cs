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
    public class DynamicFileProvider : IFileProvider
    {
        PhysicalFileProvider PhysicalFileProvider { get; set; }
        public DynamicFileProvider(string root)
        {
            if (!string.IsNullOrWhiteSpace(root) && !Directory.Exists(Path.GetFullPath(root)))
            {
                Console.WriteLine(root + " path is invalid, load temporary path");
                root = Path.GetTempPath();
            }
            PhysicalFileProvider = new PhysicalFileProvider(string.IsNullOrWhiteSpace(root) ? Path.GetTempPath() : root);
        }

        public IDirectoryContents GetDirectoryContents(string subpath)
        {
            return ((IFileProvider)PhysicalFileProvider).GetDirectoryContents(subpath);
        }

        public IFileInfo GetFileInfo(string subpath)
        {
            return ((IFileProvider)PhysicalFileProvider).GetFileInfo(subpath);
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
