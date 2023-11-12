using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Primitives;

namespace Sorter.Data
{
    public class SourceDFP : DynamicFileProvider
    {
        public SourceDFP(string root, ILogger logger) : base(root, logger)
        {
        }
    }
    public class DestinationDFP : DynamicFileProvider
    {
        public DestinationDFP(string root, ILogger logger) : base(root, logger)
        {
        }
    }
    public class TempDFP : DynamicFileProvider
    {
        public TempDFP(string root, ILogger logger) : base(root, logger)
        {
            if (string.IsNullOrWhiteSpace(root))
            {
                root = "Temp";
            }
            string path = Path.GetFullPath(root);
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
                _logger.LogInformation("Creating folder for thumbnails in " + path);
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
                _logger.LogInformation("Clearing temporary directory at " + path);
            }
            UpdateProvider(path);
        }
    }
    public class DynamicFileProvider : IFileProvider
    {
        PhysicalFileProvider? PhysicalFileProvider { get; set; }
        protected readonly ILogger _logger;
        public DynamicFileProvider(string root, ILogger logger)
        {
            PhysicalFileProvider = null;
            
            if (!string.IsNullOrWhiteSpace(root) && Directory.Exists(Path.GetFullPath(root)))
                PhysicalFileProvider = new PhysicalFileProvider(Path.GetFullPath(root));

            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public IDirectoryContents GetDirectoryContents(string subpath)
        {
            if (PhysicalFileProvider != null)
            {
                return PhysicalFileProvider.GetDirectoryContents(subpath);
            }
            else
            {
                return NotFoundDirectoryContents.Singleton;
            }
        }

        public IFileInfo GetFileInfo(string subpath)
        {
            try
            {
                if (PhysicalFileProvider != null)
                {
                    return PhysicalFileProvider.GetFileInfo(subpath);
                }
                else
                {
                    return new NotFoundFileInfo(subpath);
                }
            }
            catch
            {
                _logger.LogError("Exception in PhysicalFileProvider. 99% of the time it's a wrong configuration. All you have to do is set the configuration accordingly and initialize the service.");
                return new NotFoundFileInfo("FolderNotFound");
            }
        }

        public IChangeToken Watch(string filter)
        {
            if (PhysicalFileProvider != null)
            {
                return PhysicalFileProvider.Watch(filter);
            }
            else
            {
                return NullChangeToken.Singleton;
            }
        }
        public void UpdateProvider(string newPath)
        {
            PhysicalFileProvider = new PhysicalFileProvider(newPath);
        }
    }
}
