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
using Hpdi.VssPhysicalLib;
using SourceSafe;
using SourceSafe.Logical;

namespace Hpdi.VssLogicalLib
{
    /// <summary>
    /// Base class for revisions to a VSS item.
    /// </summary>
    /// <author>Trevor Robinson</author>
    public abstract class VssRevision
    {
        protected readonly VssItem item;
        protected readonly RevisionRecord revision;
        protected readonly CommentRecord comment;

        public VssItem Item => item;

        public VssActionBase Action { get; init; }

        public int Version => revision.Revision;

        public DateTime DateTime => revision.DateTime;

        public string User => revision.User;

        public string Label => revision.Label;

        public string Comment => comment?.Comment;

        internal VssRevision(VssItem item, RevisionRecord revision, CommentRecord comment)
        {
            this.item = item;
            this.Action = CreateAction(revision, item);
            this.revision = revision;
            this.comment = comment;
        }

        private static VssActionBase CreateAction(RevisionRecord revision, VssItem item)
        {
            VssDatabase db = item.Database;
            switch (revision.Action)
            {
                case VssPhysicalLib.Action.Label:
                {
                    return new VssLabelAction(revision.Label);
                }
                case VssPhysicalLib.Action.DestroyProject:
                case VssPhysicalLib.Action.DestroyFile:
                {
                    var destroy = (DestroyRevisionRecord)revision;
                    return new VssDestroyAction(db.GetItemName(destroy.Name, destroy.Physical));
                }
                case VssPhysicalLib.Action.RenameProject:
                case VssPhysicalLib.Action.RenameFile:
                {
                    var rename = (RenameRevisionRecord)revision;
                    return new VssRenameAction(db.GetItemName(rename.Name, rename.Physical),
                        db.GetFullName(rename.OldName));
                }
                case VssPhysicalLib.Action.MoveFrom:
                {
                    var moveFrom = (MoveRevisionRecord)revision;
                    return new VssMoveFromAction(db.GetItemName(moveFrom.Name, moveFrom.Physical),
                        moveFrom.ProjectPath);
                }
                case VssPhysicalLib.Action.MoveTo:
                {
                    var moveTo = (MoveRevisionRecord)revision;
                    return new VssMoveToAction(db.GetItemName(moveTo.Name, moveTo.Physical),
                        moveTo.ProjectPath);
                }
                case VssPhysicalLib.Action.ShareFile:
                {
                    var share = (ShareRevisionRecord)revision;

                    short pinnedRevision = share.PinnedRevision; // >0: pinned version, ==0 unpinned
                    short unpinnedRevision = share.UnpinnedRevision; // -1: shared, 0: pinned; >0 unpinned version

                    bool pinned = ((unpinnedRevision == -1 || unpinnedRevision == 0) && pinnedRevision > 0);

                    return new VssShareAction(db.GetItemName(share.Name, share.Physical), share.ProjectPath, pinned, pinned ? pinnedRevision : 0);
                }
                case VssPhysicalLib.Action.BranchFile:
                case VssPhysicalLib.Action.CreateBranch:
                {
                    var branch = (BranchRevisionRecord)revision;
                    string name = db.GetFullName(branch.Name);
                    return new VssBranchAction(
                        new VssItemName(name, branch.Physical, branch.Name.IsProject),
                        new VssItemName(name, branch.BranchFile, branch.Name.IsProject));
                }
                case VssPhysicalLib.Action.EditFile:
                {
                    return new VssEditAction(item.PhysicalName);
                }
                case VssPhysicalLib.Action.CreateProject:
                case VssPhysicalLib.Action.CreateFile:
                {
                    var create = (CommonRevisionRecord)revision;
                    return new VssCreateAction(db.GetItemName(create.Name, create.Physical));
                }
                case VssPhysicalLib.Action.AddProject:
                case VssPhysicalLib.Action.AddFile:
                {
                    var add = (CommonRevisionRecord)revision;
                    return new VssAddAction(db.GetItemName(add.Name, add.Physical));
                }
                case VssPhysicalLib.Action.DeleteProject:
                case VssPhysicalLib.Action.DeleteFile:
                {
                    var delete = (CommonRevisionRecord)revision;
                    return new VssDeleteAction(db.GetItemName(delete.Name, delete.Physical));
                }
                case VssPhysicalLib.Action.RecoverProject:
                case VssPhysicalLib.Action.RecoverFile:
                {
                    var recover = (CommonRevisionRecord)revision;
                    return new VssRecoverAction(db.GetItemName(recover.Name, recover.Physical));
                }
                case VssPhysicalLib.Action.ArchiveProject:
                {
                    var archive = (ArchiveRevisionRecord)revision;
                    return new VssArchiveAction(db.GetItemName(archive.Name, archive.Physical),
                        archive.ArchivePath);
                }
                case VssPhysicalLib.Action.RestoreProject:
                case VssPhysicalLib.Action.RestoreFile:
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
