namespace SourceSafe.Logical.Actions
{
    /// <summary>
    /// Represents a VSS label action.
    /// </summary>
    public sealed class VssLabelAction : VssActionBase
    {
        public override VssActionType Type => VssActionType.Label;

        public string Label { get; init; }

        public VssLabelAction(string label)
        {
            Label = label;
        }

        public override string ToString() => $"Label {Label}";
    };
}
