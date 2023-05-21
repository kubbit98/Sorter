using System.ComponentModel.DataAnnotations;

namespace Sorter.Data
{
    public class ConfigOptions
    {
        public const string config = "config";

        public string Source { get; set; } = Path.GetTempPath();
        public string Destination { get; set; } = Path.GetTempPath();
        public string[] ExcludeDirs { get; set; } = Array.Empty<string>();
        public bool UseWhiteListInsteadOfBlackList { get; set; } = false;
        public string[] WhiteList { get; set; } = Array.Empty<string>();
        public string[] BlackList { get; set; } = Array.Empty<string>();
        public string Password { get; set; } = string.Empty;
        public bool AllowRename { get; set; } = true;
    }
}
