using System.Text.Json;

namespace Sorter.Data
{
    public class ConfigOptionsService
    {
        private readonly DestinationDFP _destinationDFP;
        private readonly SourceDFP _sourceDFP;

        public ConfigOptionsService(DestinationDFP destinationDFP, SourceDFP sourceDFP)
        {
            _destinationDFP = destinationDFP;
            _sourceDFP = sourceDFP;
        }
        public bool CheckPath(string path)
        {
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
                jsonString = String.Concat("{\"config\": ", jsonString, "}");
                System.IO.File.WriteAllText("config.json", jsonString);
                _sourceDFP.UpdateProvider(config.Source);
                _destinationDFP.UpdateProvider(config.Destination);
            }
            catch
            {
                throw new Exception("Error upon writing the configuration");
            }
        }
        public void LoadDefaultTestConfig()
        {
            System.IO.File.Copy("..\\default_config.json", "config.json", true);
        }
    }
}
