
namespace SourceSafe.Logical
{
    /// <summary>
    /// Represents a VSS file share action.
    /// </summary>
    public sealed class VssShareAction : VssNamedActionBase
    {
        public override VssActionType Type => VssActionType.Share;

        public string OriginalProject { get; }

        public bool Pinned { get; }
        public int Revision { get; }

        public VssShareAction(VssItemName name, string originalProject, bool pinned, int revision)
            : base(name)
        {
            OriginalProject = originalProject;
            Pinned = pinned;
            Revision = revision;
        }

        public override string ToString() =>
            $"Share {Name} from {OriginalProject}, {(Pinned ? "Pin" : "Unpin")} at revision {Revision}";
    };
}
