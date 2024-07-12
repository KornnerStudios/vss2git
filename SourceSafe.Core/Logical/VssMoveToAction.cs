
namespace SourceSafe.Logical
{
    /// <summary>
    /// Represents a VSS project move-to action.
    /// </summary>
    public sealed class VssMoveToAction : VssNamedActionBase
    {
        public override VssActionType Type => VssActionType.MoveTo;

        public string NewProject { get; }

        public VssMoveToAction(VssItemName name, string newProject)
            : base(name)
        {
            NewProject = newProject;
        }

        public override string ToString() => $"Move {Name} to {NewProject}";
    };
}
