
namespace SourceSafe.Analysis.PathMapping
{
    /// <summary>
    /// Class for representing VSS pin
    /// </summary>
    public record struct VssPinInfo(
        bool Pinned,
        int Revision,
        bool Destroyed)
    {
    };
}
