using SourceSafe.Logical.Actions;
using SourceSafe.Physical.Records;
using SourceSafe.Physical.Revisions;

namespace SourceSafe.Logical.Items
{
    /// <summary>
    /// Base class for revisions to a VSS item.
    /// </summary>
    public abstract class VssItemRevisionBase
    {
        protected readonly VssItemBase mItem;
        protected readonly RevisionRecordBase mRevision;
        protected readonly CommentRecord mComment;

        public VssActionBase Action { get; init; }

        public VssItemBase Item => mItem;

        public int Version => mRevision.Revision;

        public DateTime DateTime => mRevision.DateTime;

        public string? UserName => mRevision.UserName;

        public string? Label => mRevision.Label;

        public string? Comment => mComment?.Comment;

        internal VssItemRevisionBase(
            VssItemBase item,
            RevisionRecordBase revision,
            CommentRecord comment)
        {
            mItem = item;
            mRevision = revision;
            mComment = comment;

            Action = CreateAction(revision, item);
        }

        private static VssActionBase CreateAction(
            RevisionRecordBase revision,
            VssItemBase item)
        {
            VssDatabase db = item.Database;
            switch (revision.Action)
            {
                case RevisionAction.Label:
                {
                    return new VssLabelAction(revision.Label!);
                }
                case RevisionAction.DestroyProject:
                case RevisionAction.DestroyFile:
                {
                    var destroy = (DestroyRevisionRecord)revision;
                    return new VssDestroyAction(db.GetItemName(destroy.Name, destroy.Physical));
                }
                case RevisionAction.RenameProject:
                case RevisionAction.RenameFile:
                {
                    var rename = (RenameRevisionRecord)revision;
                    return new VssRenameAction(db.GetItemName(rename.Name, rename.Physical),
                        db.GetFullName(rename.OldName));
                }
                case RevisionAction.MoveFrom:
                {
                    var moveFrom = (MoveRevisionRecord)revision;
                    return new VssMoveFromAction(db.GetItemName(moveFrom.Name, moveFrom.Physical),
                        moveFrom.ProjectPath);
                }
                case RevisionAction.MoveTo:
                {
                    var moveTo = (MoveRevisionRecord)revision;
                    return new VssMoveToAction(db.GetItemName(moveTo.Name, moveTo.Physical),
                        moveTo.ProjectPath);
                }
                case RevisionAction.ShareFile:
                {
                    var share = (ShareRevisionRecord)revision;

                    short pinnedRevision = share.PinnedRevision; // >0: pinned version, ==0 unpinned
                    short unpinnedRevision = share.UnpinnedRevision; // -1: shared, 0: pinned; >0 unpinned version

                    bool pinned = ((unpinnedRevision == -1 || unpinnedRevision == 0) && pinnedRevision > 0);

                    return new VssShareAction(db.GetItemName(share.Name, share.Physical), share.ProjectPath, pinned, pinned ? pinnedRevision : 0);
                }
                case RevisionAction.BranchFile:
                case RevisionAction.CreateBranch:
                {
                    var branch = (BranchRevisionRecord)revision;
                    string name = db.GetFullName(branch.Name);
                    return new VssBranchAction(
                        new VssItemName(name, branch.Physical, branch.Name.IsProject),
                        new VssItemName(name, branch.BranchFile, branch.Name.IsProject));
                }
                case RevisionAction.EditFile:
                {
                    return new VssEditAction(item.PhysicalName);
                }
                case RevisionAction.CreateProject:
                case RevisionAction.CreateFile:
                {
                    var create = (CommonRevisionRecord)revision;
                    return new VssCreateAction(db.GetItemName(create.Name, create.Physical));
                }
                case RevisionAction.AddProject:
                case RevisionAction.AddFile:
                {
                    var add = (CommonRevisionRecord)revision;
                    return new VssAddAction(db.GetItemName(add.Name, add.Physical));
                }
                case RevisionAction.DeleteProject:
                case RevisionAction.DeleteFile:
                {
                    var delete = (CommonRevisionRecord)revision;
                    return new VssDeleteAction(db.GetItemName(delete.Name, delete.Physical));
                }
                case RevisionAction.RecoverProject:
                case RevisionAction.RecoverFile:
                {
                    var recover = (CommonRevisionRecord)revision;
                    return new VssRecoverAction(db.GetItemName(recover.Name, recover.Physical));
                }
                case RevisionAction.ArchiveProject:
                {
                    var archive = (ArchiveRevisionRecord)revision;
                    return new VssArchiveAction(db.GetItemName(archive.Name, archive.Physical),
                        archive.ArchivePath);
                }
                case RevisionAction.RestoreProject:
                case RevisionAction.RestoreFile:
                {
                    var archive = (ArchiveRevisionRecord)revision;
                    return new VssRestoreAction(db.GetItemName(archive.Name, archive.Physical),
                        archive.ArchivePath);
                }
                default:
                    throw new ArgumentException($"Unknown revision action: {revision.Action}");
            }
        }
    };
}
