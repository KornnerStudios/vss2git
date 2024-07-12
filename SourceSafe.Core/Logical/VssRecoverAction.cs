
namespace SourceSafe.Logical
{
    /// <summary>
    /// Represents a VSS project/file recover action.
    /// </summary>
    public sealed class VssRecoverAction : VssNamedActionBase
    {
        public override VssActionType Type => VssActionType.Recover;

        public VssRecoverAction(VssItemName name)
            : base(name)
        {
        }

        public override string ToString() => $"Recover {Name}";
    };
}
