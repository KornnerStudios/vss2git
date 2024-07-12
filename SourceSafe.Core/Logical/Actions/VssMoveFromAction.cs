namespace SourceSafe.Logical.Actions
{
    /// <summary>
    /// Represents a VSS project move-from action.
    /// </summary>
    public sealed class VssMoveFromAction : VssNamedActionBase
    {
        public override VssActionType Type => VssActionType.MoveFrom;

        public string OriginalProject { get; }

        public VssMoveFromAction(VssItemName name, string originalProject)
            : base(name)
        {
            OriginalProject = originalProject;
        }

        public override string ToString() => $"Move {Name} from {OriginalProject}";
    };
}
