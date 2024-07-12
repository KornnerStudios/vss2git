namespace SourceSafe.Logical.Actions
{
    /// <summary>
    /// Represents a VSS project/file rename action.
    /// </summary>
    public sealed class VssRenameAction : VssNamedActionBase
    {
        public override VssActionType Type => VssActionType.Rename;

        public string OriginalName { get; init; }

        public VssRenameAction(VssItemName name, string originalName)
            : base(name)
        {
            OriginalName = originalName;
        }

        public override string ToString() => $"Rename {OriginalName} to {Name}";
    };
}
