﻿using Microsoft.Extensions.FileProviders;
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
            if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(Path.GetFullPath(root)))
            {
                Console.WriteLine(Path.GetFullPath(root) + " path is invalid, load temporary path");
                root = Path.GetTempPath();
            }
            PhysicalFileProvider = new PhysicalFileProvider(Path.GetFullPath(root));
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
