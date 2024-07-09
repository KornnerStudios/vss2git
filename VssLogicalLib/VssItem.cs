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
using Hpdi.VssPhysicalLib;

namespace Hpdi.VssLogicalLib
{
    /// <summary>
    /// Represents an abstract VSS item, which is a project or file.
    /// </summary>
    /// <author>Trevor Robinson</author>
    public abstract class VssItem
    {
        private ItemFile itemFile;

        public VssDatabase Database { get; init; }

        public VssItemName ItemName { get; init; }

        public bool IsProject => ItemName.IsProject;

        public string Name => ItemName.LogicalName;

        public string PhysicalName => ItemName.PhysicalName;

        public string PhysicalPath { get; init; }

        public string DataPath => PhysicalPath + ItemFile.Header.DataExt;

        public int RevisionCount => ItemFile.Header.Revisions;

        public IEnumerable<VssRevision> Revisions => new VssRevisions<VssItem, VssRevision>(this);

        public VssRevision GetRevision(int version)
        {
            ItemFile itemFile = ItemFile;
            if (version < 1 || version > itemFile.Header.Revisions)
            {
                throw new ArgumentOutOfRangeException(nameof(version), version, "Invalid version number");
            }

            // check whether version was before branch
            if (version < itemFile.Header.FirstRevision)
            {
                if (!IsProject)
                {
                    var fileHeader = (FileHeaderRecord)itemFile.Header;
                    return Database.GetItemPhysical(fileHeader.BranchFile).GetRevision(version);
                }
                else
                {
                    // should never happen; projects cannot branch
                    throw new ArgumentOutOfRangeException(nameof(version), version, "Undefined version");
                }
            }

            RevisionRecord revisionRecord = itemFile.GetFirstRevision();
            while (revisionRecord != null && revisionRecord.Revision < version)
            {
                revisionRecord = itemFile.GetNextRevision(revisionRecord);
            }
            if (revisionRecord == null)
            {
                throw new ArgumentException("Version not found", nameof(version));
            }
            return CreateRevision(revisionRecord);
        }

        internal ItemFile ItemFile
        {
            get
            {
                if (itemFile == null)
                {
                    itemFile = new ItemFile(PhysicalPath, Database.Encoding);
                }
                return itemFile;
            }
            set
            {
                itemFile = value;
            }
        }

        internal VssItem(VssDatabase database, VssItemName itemName, string physicalPath)
        {
            Database = database;
            ItemName = itemName;
            PhysicalPath = physicalPath;
        }

        protected VssRevision CreateRevision(RevisionRecord revision)
        {
            CommentRecord comment = null;
            if (revision.CommentLength > 0 && revision.CommentOffset > 0)
            {
                comment = new CommentRecord();
                ItemFile.ReadRecord(comment, revision.CommentOffset);
            }
            else if (revision.Action == VssPhysicalLib.Action.Label &&
                revision.LabelCommentLength > 0 && revision.LabelCommentOffset > 0)
            {
                comment = new CommentRecord();
                ItemFile.ReadRecord(comment, revision.LabelCommentOffset);
            }
            return CreateRevision(revision, comment);
        }

        protected abstract VssRevision CreateRevision(RevisionRecord revision, CommentRecord comment);

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
            private RevisionRecord revisionRecord;
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
                    revisionRecord = item.ItemFile.GetFirstRevision();
                    beforeFirst = false;
                }
                else if (revisionRecord != null)
                {
                    revisionRecord = item.ItemFile.GetNextRevision(revisionRecord);
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
