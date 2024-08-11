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
using System.Collections;
using System.Collections.Generic;
using SourceSafe.Logical;
using SourceSafe.Physical.Files;
using SourceSafe.Physical.Records;

namespace Hpdi.VssLogicalLib
{
    /// <summary>
    /// Represents an abstract VSS item, which is a project or file.
    /// </summary>
    /// <author>Trevor Robinson</author>
    public abstract class VssItem
    {
        private VssPhysicalFile mPhysicalFile;

        public VssDatabase Database { get; init; }

        public VssItemName ItemName { get; init; }

        public bool IsProject => ItemName.IsProject;

        public string Name => ItemName.LogicalName;

        public string PhysicalName => ItemName.PhysicalName;

        public string PhysicalPath { get; init; }

        public string DataPath => PhysicalPath + PhysicalFile.Header.DataExt;

        public int RevisionCount => PhysicalFile.Header.Revisions;

        public IEnumerable<VssRevision> Revisions => new VssRevisions<VssItem, VssRevision>(this);

        public VssRevision GetRevision(int version)
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

            SourceSafe.Physical.Revisions.RevisionRecordBase revisionRecord = backingPhysicalFile.GetFirstRevision();
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
                    mPhysicalFile = new(PhysicalPath, Database.Encoding);
                }
                return mPhysicalFile;
            }
            set
            {
                mPhysicalFile = value;
            }
        }

        internal VssItem(VssDatabase database, VssItemName itemName, string physicalPath)
        {
            Database = database;
            ItemName = itemName;
            PhysicalPath = physicalPath;
        }

        protected VssRevision CreateRevision(SourceSafe.Physical.Revisions.RevisionRecordBase revision)
        {
            CommentRecord comment = null;
            if (revision.CommentLength > 0 && revision.CommentOffset > 0)
            {
                comment = new CommentRecord();
                PhysicalFile.ReadRecord(comment, revision.CommentOffset);
            }
            else if (revision.Action == SourceSafe.Physical.Revisions.RevisionAction.Label &&
                revision.LabelCommentLength > 0 && revision.LabelCommentOffset > 0)
            {
                comment = new CommentRecord();
                PhysicalFile.ReadRecord(comment, revision.LabelCommentOffset);
            }
            return CreateRevision(revision, comment);
        }

        protected abstract VssRevision CreateRevision(SourceSafe.Physical.Revisions.RevisionRecordBase revision, CommentRecord comment);

        protected class VssRevisions<ItemT, RevisionT> : IEnumerable<RevisionT>
            where ItemT : VssItem
            where RevisionT : VssRevision
        {
            private readonly ItemT item;

            internal VssRevisions(ItemT item) => this.item = item;

            public IEnumerator<RevisionT> GetEnumerator() => new VssRevisionEnumerator<ItemT, RevisionT>(item);

            IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();
        }

        private class VssRevisionEnumerator<ItemT, RevisionT> : IEnumerator<RevisionT>
            where ItemT : VssItem
            where RevisionT : VssRevision
        {
            private readonly ItemT item;
            private SourceSafe.Physical.Revisions.RevisionRecordBase revisionRecord;
            private RevisionT revision;
            private bool beforeFirst = true;
            private int revisionIndex = -1;

            internal VssRevisionEnumerator(ItemT item) => this.item = item;

            public void Dispose()
            {
            }

            public void Reset()
            {
                beforeFirst = true;
            }

            public bool MoveNext()
            {
                revision = null;
                int nextRevisionIndex = revisionIndex + 1;

                if (beforeFirst)
                {
                    revisionRecord = item.PhysicalFile.GetFirstRevision();
                    beforeFirst = false;
                }
                else if (revisionRecord != null)
                {
                    revisionRecord = item.PhysicalFile.GetNextRevision(revisionRecord);
                }

                if (revisionRecord != null)
                {
                    revisionIndex = nextRevisionIndex;
                }

                return revisionRecord != null;
            }

            public RevisionT Current
            {
                get
                {
                    if (revisionRecord == null)
                    {
                        throw new InvalidOperationException();
                    }

                    if (revision == null)
                    {
                        revision = (RevisionT)item.CreateRevision(revisionRecord);
                    }

                    return revision;
                }
            }

            object IEnumerator.Current => this.Current;
        }
    }
}
