namespace SourceSafe.Logical.Actions
{
    /// <summary>
    /// Represents a VSS file pin/unpin action.
    /// </summary>
    public sealed class VssPinAction : VssNamedActionBase
    {
        public override VssActionType Type => VssActionType.Pin;

        public bool Pinned { get; }
        public int Revision { get; }

        public VssPinAction(VssItemName name, bool pinned, int revision)
            : base(name)
        {
            Pinned = pinned;
            Revision = revision;
        }

        public override string ToString() =>
            $"{(Pinned ? "Pin" : "Unpin")} {Name} at revision {Revision}";
    };
}
