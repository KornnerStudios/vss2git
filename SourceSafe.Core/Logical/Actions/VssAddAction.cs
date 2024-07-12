namespace SourceSafe.Logical.Actions
{
    /// <summary>
    /// Represents a VSS project/file add action.
    /// </summary>
    public sealed class VssAddAction : VssNamedActionBase
    {
        public override VssActionType Type => VssActionType.Add;

        public VssAddAction(VssItemName name)
            : base(name)
        {
        }

        public override string ToString() => $"Add {Name}";
    };
}
