﻿
namespace SourceSafe.Logical
{
    /// <summary>
    /// Represents a VSS restore from archive action.
    /// </summary>
    public sealed class VssRestoreAction : VssNamedActionBase
    {
        public override VssActionType Type => VssActionType.Restore;

        public string ArchivePath { get; }

        public VssRestoreAction(VssItemName name, string archivePath)
            : base(name)
        {
            this.ArchivePath = archivePath;
        }

        public override string ToString() => $"Restore {Name} from archive {ArchivePath}";
    };
}
