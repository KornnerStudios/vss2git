/* Copyright 2009 HPDI, LLC
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

using System;
using SourceSafe.Logical;
using SourceSafe.Logical.Actions;
using SourceSafe.Physical.Records;
using SourceSafe.Physical.Revisions;

namespace Hpdi.VssLogicalLib
{
    /// <summary>
    /// Base class for revisions to a VSS item.
    /// </summary>
    /// <author>Trevor Robinson</author>
    public abstract class VssRevision
    {
        protected readonly VssItem item;
        protected readonly RevisionRecordBase revision;
        protected readonly CommentRecord comment;

        public VssItem Item => item;

        public VssActionBase Action { get; init; }

        public int Version => revision.Revision;

        public DateTime DateTime => revision.DateTime;

        public string User => revision.User;

        public string Label => revision.Label;

        public string Comment => comment?.Comment;

        internal VssRevision(VssItem item, RevisionRecordBase revision, CommentRecord comment)
        {
            this.item = item;
            this.Action = CreateAction(revision, item);
            this.revision = revision;
            this.comment = comment;
        }

        private static VssActionBase CreateAction(RevisionRecordBase revision, VssItem item)
        {
            VssDatabase db = item.Database;
            switch (revision.Action)
            {
                case RevisionAction.Label:
                {
                    return new VssLabelAction(revision.Label);
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
    }
}
