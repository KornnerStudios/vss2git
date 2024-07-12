
namespace SourceSafe
{
    public static class SourceSafeConstants
    {
        public const string RootProjectName = "$";
        public const string RootPhysicalFile = "AAAAAAAA";
        public const char ProjectSeparatorChar = '/';
        public const string ProjectSeparator = "/";

        public const string ProjectSpecPrefix = RootProjectName + ProjectSeparator;

        public const string IniFile = "srcsafe.ini";

        public static readonly DateTime UnixLocalTimeEpoch =
            new(1970, 1, 1, 0, 0, 0, DateTimeKind.Local);
    };
}
