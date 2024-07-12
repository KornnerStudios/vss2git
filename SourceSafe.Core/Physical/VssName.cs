
namespace SourceSafe.Physical
{
    /// <summary>
    /// Structure used to store a VSS project or file name.
    /// </summary>
    public readonly struct VssName(short flags, string shortName, int nameFileOffset)
    {
        public const int ShortNameLength = 34;
        public const int Length = 2 + ShortNameLength + 4;

        public bool IsProject => (flags & 1) != 0;

        public string ShortName => shortName;

        public int NameFileOffset => nameFileOffset;
    };
}
