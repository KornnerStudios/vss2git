
namespace SourceSafe.Logical
{
    /// <summary>
    /// Represents a VSS project/file delete action.
    /// </summary>
    public sealed class VssDeleteAction : VssNamedActionBase
    {
        public override VssActionType Type => VssActionType.Delete;

        public VssDeleteAction(VssItemName name)
            : base(name)
        {
        }

        public override string ToString() => $"Delete {Name}";
    };
}
