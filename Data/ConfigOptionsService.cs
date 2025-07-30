using Microsoft.Extensions.Options;
using System.Text.Json;

namespace Sorter.Data
{
    public class ConfigOptionsService(DestinationDFP destinationDFP, SourceDFP sourceDFP, IOptionsMonitor<ConfigOptions> configOptionsMonitor, IOptionsMonitor<KeyBindsOptions> keyBindsOptions)
    {
        private readonly DestinationDFP _destinationDFP = destinationDFP;
        private readonly SourceDFP _sourceDFP = sourceDFP;
        private readonly IOptionsMonitor<ConfigOptions> _configOptionsMonitor = configOptionsMonitor;
        private readonly IOptionsMonitor<KeyBindsOptions> _keyBindsOptions = keyBindsOptions;

        public bool CheckPath(string path)
        {
            if (string.IsNullOrEmpty(path)) return false;
            return Directory.Exists(Path.GetFullPath(path));
        }
        public string GetPathIfValid(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return string.Empty;
            return Directory.Exists(Path.GetFullPath(path)) ? Path.GetFullPath(path) : string.Empty;
        }
        public void SaveOptions(ConfigOptions config)
        {
            if (!CheckPath(config.Source))
            {
                throw new ArgumentException("Invalid path in source");
            }
            if (!CheckPath(config.Destination))
            {
                throw new ArgumentException("Invalid path in destination");
            }
            try
            {
                string jsonString = JsonSerializer.Serialize(config);
                jsonString = string.Concat("{\"config\": ", jsonString, "}");
                System.IO.File.WriteAllText("config.json", jsonString);
                _sourceDFP.UpdateProvider(Path.GetFullPath(config.Source));
                _destinationDFP.UpdateProvider(Path.GetFullPath(config.Destination));
            }
            catch
            {
                throw new Exception("Error upon writing the configuration");
            }
        }
        public void SaveKeyBinds(KeyBindsOptions keyBindsOptions)
        {
            try
            {
                string jsonString = JsonSerializer.Serialize(keyBindsOptions);
                jsonString = string.Concat("{\"KeyBinds\": ", jsonString, "}");
                System.IO.File.WriteAllText("keybinds.json", jsonString);
            }
            catch
            {
                throw new Exception("Error upon writing the configuration");
            }
        }
        public void LoadDefaultTestConfig()
        {
            System.IO.File.Copy(Path.Combine("TestFolder", "test_config.json"), "config.json", true);
        }
        public void RearrangeTestFiles()
        {
            string dst = Path.Combine("TestFolder", "dst");
            if (!Directory.Exists(dst))
            {
                Directory.CreateDirectory(dst);
            }
            string src = Path.Combine("TestFolder", "src");
            if (!Directory.Exists(src))
            {
                Directory.CreateDirectory(src);
            }

            DirectoryInfo di = new(dst);
            foreach (FileInfo file in di.GetFiles())
            {
                file.Delete();
            }
            foreach (DirectoryInfo dir in di.GetDirectories())
            {
                dir.Delete(true);
            }
            for (char c = 'A'; c <= 'E'; c++)
            {
                Directory.CreateDirectory(Path.Combine(dst, c.ToString()));
            }
            for (int i = 0; i <= 9; i++)
            {
                string path = Path.Combine(src, i + ".txt");
                if (!System.IO.File.Exists(path))
                {
                    using StreamWriter sw = System.IO.File.CreateText(path);
                    sw.WriteLine("File number");
                    sw.WriteLine(i);
                    sw.WriteLine(":P");
                }
            }
        }
        public bool CheckMonitor(ConfigOptions config)
        {
            if (!_configOptionsMonitor.CurrentValue.Source.Equals(config.Source)) return false;
            if (!_configOptionsMonitor.CurrentValue.ExcludeDirsSource.SequenceEqual(config.ExcludeDirsSource)) return false;
            if (!_configOptionsMonitor.CurrentValue.Destination.Equals(config.Destination)) return false;
            if (!_configOptionsMonitor.CurrentValue.ExcludeDirsDestination.SequenceEqual(config.ExcludeDirsDestination)) return false;
            if (!_configOptionsMonitor.CurrentValue.UseWhiteListInsteadOfBlackList.Equals(config.UseWhiteListInsteadOfBlackList)) return false;
            if (!_configOptionsMonitor.CurrentValue.WhiteList.SequenceEqual(config.WhiteList)) return false;
            if (!_configOptionsMonitor.CurrentValue.BlackList.SequenceEqual(config.BlackList)) return false;
            if (!_configOptionsMonitor.CurrentValue.AllowRename.Equals(config.AllowRename)) return false;
            return true;
        }
        public bool CheckMonitor(KeyBindsOptions config)
        {
            return config.KeyBinds.OrderBy(kvp => kvp.Key).SequenceEqual(_keyBindsOptions.CurrentValue.KeyBinds.OrderBy(kvp => kvp.Key));
        }
    }
}
