
namespace SourceSafe.Logical
{
    /// <summary>
    /// Represents a VSS project/file destroy action.
    /// </summary>
    public sealed class VssDestroyAction : VssNamedActionBase
    {
        public override VssActionType Type => VssActionType.Destroy;

        public VssDestroyAction(VssItemName name)
            : base(name)
        {
        }

        public override string ToString() => $"Destroy {Name}";
    };
}
