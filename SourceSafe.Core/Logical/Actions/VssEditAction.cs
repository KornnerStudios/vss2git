namespace SourceSafe.Logical.Actions
{
    /// <summary>
    /// Represents a VSS file edit action.
    /// </summary>
    public sealed class VssEditAction : VssActionBase
    {
        public override VssActionType Type => VssActionType.Edit;

        public string PhysicalName { get; }

        public VssEditAction(string physicalName)
        {
            PhysicalName = physicalName;
        }

        public override string ToString() => $"Edit {PhysicalName}";
    };
}
