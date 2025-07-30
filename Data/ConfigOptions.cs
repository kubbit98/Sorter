namespace Sorter.Data
{
    public class ConfigOptions
    {
        public const string config = "config";

        public string Source { get; set; } = string.Empty;
        public string[] ExcludeDirsSource { get; set; } = [];
        public string Destination { get; set; } = string.Empty;
        public string[] ExcludeDirsDestination { get; set; } = [];
        public bool UseWhiteListInsteadOfBlackList { get; set; } = false;
        public string[] WhiteList { get; set; } = [];
        public string[] BlackList { get; set; } = [];
        public bool ShowSidePanel { get; set; } = true;
        public bool AllowRename { get; set; } = true;
    }
}
