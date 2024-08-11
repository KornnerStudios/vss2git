
namespace SourceSafe.Physical.Projects
{
    /// <summary>
    /// Flags enumeration for items in project.
    /// </summary>
    [Flags]
    public enum ProjectEntryFlags
    {
        Deleted = 0x01,
        Binary = 0x02,
        LatestOnly = 0x04,
        Shared = 0x08,
    };
}
