using Microsoft.Extensions.Options;
using System.Diagnostics;
using System.Text.Json;

namespace Sorter.Data
{
    public class ConfigOptionsService(DestinationDFP destinationDFP, SourceDFP sourceDFP, IOptionsMonitor<ConfigOptions> configOptionsMonitor, IOptionsMonitor<KeyBindsOptions> keyBindsOptions, IConfiguration configuration)
    {
        private static readonly TimeSpan s_applyTimeout = TimeSpan.FromSeconds(5);
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
                ReloadConfiguration();
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
                ReloadConfiguration();
            }
            catch
            {
                throw new Exception("Error upon writing the configuration");
            }
        }
        /// <summary>Returns the loaded configuration, so callers can wait until it's applied.</summary>
        public ConfigOptions LoadDefaultTestConfig()
        {
            string json = System.IO.File.ReadAllText(Path.Combine("TestFolder", "test_config.json"));
            ConfigOptions config = JsonSerializer.Deserialize<Dictionary<string, ConfigOptions>>(json)![ConfigOptions.config];
            // SaveOptions also validates the paths and points static file providers at the test folders
            SaveOptions(config);
            return config;
        }
        /// <summary>
        /// Waits until the options monitor returns the saved values. False after the timeout,
        /// then the file is saved but the application still uses the previous values.
        /// </summary>
        public Task<bool> WaitUntilAppliedAsync(ConfigOptions config, TimeSpan? timeout = null) =>
            WaitUntilAsync(() => CheckMonitor(config), timeout ?? s_applyTimeout);
        public Task<bool> WaitUntilAppliedAsync(KeyBindsOptions config, TimeSpan? timeout = null) =>
            WaitUntilAsync(() => CheckMonitor(config), timeout ?? s_applyTimeout);
        private static async Task<bool> WaitUntilAsync(Func<bool> condition, TimeSpan timeout)
        {
            var stopwatch = Stopwatch.StartNew();
            while (!condition())
            {
                if (stopwatch.Elapsed > timeout) return false;
                await Task.Delay(50);
            }
            return true;
        }
        // reloadOnChange relies on file system notifications, which don't work everywhere (e.g. network drives)
        private void ReloadConfiguration() => (configuration as IConfigurationRoot)?.Reload();
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
