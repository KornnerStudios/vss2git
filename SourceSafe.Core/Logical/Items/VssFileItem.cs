using SourceSafe.Physical.Records;

namespace SourceSafe.Logical.Items
{
    /// <summary>
    /// Represents a VSS file.
    /// </summary>
    public sealed class VssFileItem : VssItemBase
    {
        internal VssItemFileHeaderRecord Header
            => (VssItemFileHeaderRecord)PhysicalFile.Header;

        public bool IsLocked => (Header.Flags & VssItemFileFlags.Locked) != 0;

        public bool IsBinary => (Header.Flags & VssItemFileFlags.Binary) != 0;

        public bool IsLatestOnly => (Header.Flags & VssItemFileFlags.LatestOnly) != 0;

        public bool IsShared => (Header.Flags & VssItemFileFlags.Shared) != 0;

        public bool IsCheckedOut => (Header.Flags & VssItemFileFlags.CheckedOut) != 0;

        public uint Crc => Header.DataCrc;

        public DateTime LastRevised => Header.LastRevDateTime;

        public DateTime LastModified => Header.ModificationDateTime;

        public DateTime Created => Header.CreationDateTime;

        public new IEnumerable<VssFileItemRevision> Revisions
            => new VssRevisionsEnumeration<VssFileItem, VssFileItemRevision>(this);

        public new VssFileItemRevision GetRevision(int version)
            => (VssFileItemRevision)base.GetRevision(version);

        internal VssFileItem(
            VssDatabase database,
            VssItemName itemName,
            string physicalPath)
            : base(database, itemName, physicalPath)
        {
        }

        public string GetPath(VssProjectItem project)
        {
            return project.LogicalPath + SourceSafeConstants.ProjectSeparator + Name;
        }

        protected override VssItemRevisionBase CreateRevision(
            Physical.Revisions.RevisionRecordBase revision,
            CommentRecord comment)
        {
            return new VssFileItemRevision(this, revision, comment);
        }
    };
}
