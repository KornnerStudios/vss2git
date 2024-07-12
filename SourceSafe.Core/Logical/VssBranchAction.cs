
namespace SourceSafe.Logical
{
    /// <summary>
    /// Represents a VSS file branch action.
    /// </summary>
    public sealed class VssBranchAction : VssNamedActionBase
    {
        public override VssActionType Type => VssActionType.Branch;

        public VssItemName Source { get; }

        public VssBranchAction(VssItemName name, VssItemName source)
            : base(name)
        {
            Source = source;
        }

        public override string ToString() => $"Branch {Name} from {Source.PhysicalName}";
    };
}
