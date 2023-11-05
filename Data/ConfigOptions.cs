namespace Sorter.Data
{
    public class ConfigOptions
    {
        public const string config = "config";

        public string Source { get; set; } = string.Empty;
        public string[] ExcludeDirsSource { get; set; } = Array.Empty<string>();
        public string Destination { get; set; } = string.Empty;
        public string[] ExcludeDirsDestination { get; set; } = Array.Empty<string>();
        public bool UseWhiteListInsteadOfBlackList { get; set; } = false;
        public string[] WhiteList { get; set; } = Array.Empty<string>();
        public string[] BlackList { get; set; } = Array.Empty<string>();
        public bool AllowRename { get; set; } = true;
    }
}
