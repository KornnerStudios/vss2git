using SourceSafe.Physical.Files;
using SourceSafe.Physical.Records;

namespace SourceSafe.Logical.Items
{
    /// <summary>
    /// Represents an abstract VSS item, which is a project or file.
    /// </summary>
    public abstract partial class VssItemBase
    {
        private VssPhysicalFile? mPhysicalFile;

        public VssDatabase Database { get; init; }

        public VssItemName ItemName { get; init; }

        public string PhysicalPath { get; init; }

        public bool IsProject => ItemName.IsProject;

        public string Name => ItemName.LogicalName;

        public string PhysicalName => ItemName.PhysicalName;

        public string DataPath => PhysicalPath + PhysicalFile.Header.DataExt;

        public int RevisionCount => PhysicalFile.Header.Revisions;

        public IEnumerable<VssItemRevisionBase> Revisions =>
            new VssRevisionsEnumeration<VssItemBase, VssItemRevisionBase>(this);

        public VssItemRevisionBase GetRevision(int version)
        {
            VssPhysicalFile backingPhysicalFile = PhysicalFile;
            if (version < 1 || version > backingPhysicalFile.Header.Revisions)
            {
                throw new ArgumentOutOfRangeException(nameof(version), version, "Invalid version number");
            }

            // check whether version was before branch
            if (version < backingPhysicalFile.Header.FirstRevision)
            {
                if (!IsProject)
                {
                    var fileHeader = (VssItemFileHeaderRecord)backingPhysicalFile.Header;
                    return Database.GetItemByPhysicalName(fileHeader.BranchFile).GetRevision(version);
                }
                else
                {
                    // should never happen; projects cannot branch
                    throw new ArgumentOutOfRangeException(nameof(version), version, "Undefined version");
                }
            }

            Physical.Revisions.RevisionRecordBase? revisionRecord = backingPhysicalFile.GetFirstRevision();
            while (revisionRecord != null && revisionRecord.Revision < version)
            {
                revisionRecord = backingPhysicalFile.GetNextRevision(revisionRecord);
            }
            if (revisionRecord == null)
            {
                throw new ArgumentException("Version not found", nameof(version));
            }
            return CreateRevision(revisionRecord);
        }

        internal VssPhysicalFile PhysicalFile
        {
            get
            {
                if (mPhysicalFile == null)
                {
                    mPhysicalFile = new(Database, PhysicalPath);
                }
                return mPhysicalFile;
            }
            set
            {
                mPhysicalFile = value;
            }
        }

        internal VssItemBase(
            VssDatabase database,
            VssItemName itemName,
            string physicalPath)
        {
            Database = database;
            ItemName = itemName;
            PhysicalPath = physicalPath;
        }

        protected VssItemRevisionBase CreateRevision(
            Physical.Revisions.RevisionRecordBase revision)
        {
            CommentRecord? comment = null;
            if (revision.CommentLength > 0 && revision.CommentOffset > 0)
            {
                comment = new CommentRecord();
                PhysicalFile.ReadRecord(comment, revision.CommentOffset);
            }
            else if (revision.Action == Physical.Revisions.RevisionAction.Label &&
                revision.LabelCommentLength > 0 && revision.LabelCommentOffset > 0)
            {
                comment = new CommentRecord();
                PhysicalFile.ReadRecord(comment, revision.LabelCommentOffset);
            }
            return CreateRevision(revision, comment!);
        }

        protected abstract VssItemRevisionBase CreateRevision(
            Physical.Revisions.RevisionRecordBase revision,
            CommentRecord comment);
    };
}
