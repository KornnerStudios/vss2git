
namespace SourceSafe.Logical
{
    /// <summary>
    /// Represents a VSS archive action.
    /// </summary>
    public sealed class VssArchiveAction : VssNamedActionBase
    {
        public override VssActionType Type => VssActionType.Archive;

        public string ArchivePath { get; }

        public VssArchiveAction(VssItemName name, string archivePath)
            : base(name)
        {
            ArchivePath = archivePath;
        }

        public override string ToString() => $"Archive {Name} to {ArchivePath}";
    };
}
