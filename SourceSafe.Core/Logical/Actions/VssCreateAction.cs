namespace SourceSafe.Logical.Actions
{
    /// <summary>
    /// Represents a VSS project/file create action.
    /// </summary>
    public sealed class VssCreateAction : VssNamedActionBase
    {
        public override VssActionType Type => VssActionType.Create;

        public VssCreateAction(VssItemName name)
            : base(name)
        {
        }

        public override string ToString() => $"Create {Name}";
    };
}
