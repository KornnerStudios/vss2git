﻿/* Copyright 2009 HPDI, LLC
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

// https://github.com/trevorr/vss2git/pull/48#issuecomment-804139011
//      "When you change a comment of a checkin, VSS does not change the original comment record, but appends a new comment record to the physical file,
//      updating the EOF offset. Therefore the proposed fix let Vss2Git fail to analyse any physical file with a such edited comment, because the iterator
//      skips all the records between the one owning the new comment and the corresponding comment record, while building the changeset list."
// I contend that the EOF offset is valid, contrary to what this Nyk72's comment says.
#define EOF_OFFSET_CHECK_ENABLED
//
#define LAST_REVISION_OFFSET_CHECK_ENABLED

using System.Collections.Generic;
using System.Text;
using SourceSafe.Physical;
using SourceSafe.Physical.Records;
using SourceSafe.Physical.Revisions;

namespace Hpdi.VssPhysicalLib
{
    /// <summary>
    /// Represents a file containing VSS project/file records.
    /// </summary>
    /// <author>Trevor Robinson</author>
    public sealed class ItemFile : SourceSafe.Physical.Files.VssRecordFileBase
    {
        const string FILE_SIGNATUIRE = "SourceSafe@Microsoft";

        public VssItemHeaderRecordBase Header { get; }

        public ItemFile(string filename, Encoding encoding)
            : base(filename, encoding)
        {
            try
            {
                string fileSig = reader.ReadString(0x20);
                if (fileSig != FILE_SIGNATUIRE)
                {
                    throw new BadHeaderException("Incorrect file signature");
                }

                var fileType = (VssItemType)reader.ReadInt16();
                short fileVersion = reader.ReadInt16();
                if (fileVersion != 6)
                {
                    throw new BadHeaderException($"Incorrect file version: {fileVersion}");
                }

                reader.SkipAssumedToBeAllZeros(16); // reserved; always 0

                if (fileType == VssItemType.Project)
                {
                    Header = new VssItemProjectHeaderRecord();
                }
                else
                {
                    Header = new VssItemFileHeaderRecord();
                }

                ReadRecord(Header);
                if (Header.ItemType != fileType)
                {
                    throw new BadHeaderException("Header record type mismatch");
                }
            }
            catch (SourceSafe.IO.EndOfBufferException e)
            {
                throw new BadHeaderException("Truncated header", e);
            }
        }

        [System.Obsolete("Currently unused")]
        public VssRecordBase GetRecord(int offset)
        {
            return GetRecord(CreateVssRecord, false, offset);
        }

        public VssRecordBase GetNextRecord(bool skipUnknown)
        {
#if EOF_OFFSET_CHECK_ENABLED
            if (reader.Offset == this.Header.EofOffset)
            {
                return null;
            }
#endif // EOF_OFFSET_CHECK_ENABLED

#if LAST_REVISION_OFFSET_CHECK_ENABLED
            if (reader.Offset > this.Header.LastRevOffset)
            {
                return null;
            }
#endif // LAST_REVISION_OFFSET_CHECK_ENABLED

            return GetNextRecord(CreateVssRecord, skipUnknown);
        }

        public RevisionRecordBase GetFirstRevision()
        {
            if (Header.FirstRevOffset > 0)
            {
                return GetRecord(CreateRevisionRecord, false, Header.FirstRevOffset);
            }
            return null;
        }

        public RevisionRecordBase GetNextRevision(RevisionRecordBase revision)
        {
#if EOF_OFFSET_CHECK_ENABLED
            if (reader.Offset == this.Header.EofOffset)
            {
                return null;
            }
#endif // EOF_OFFSET_CHECK_ENABLED

#if LAST_REVISION_OFFSET_CHECK_ENABLED
            if (reader.Offset > this.Header.LastRevOffset)
            {
                return null;
            }
#endif // LAST_REVISION_OFFSET_CHECK_ENABLED

            reader.Offset = revision.Header.Offset + revision.Header.Length + RecordHeader.LENGTH;
            return GetNextRecord(CreateRevisionRecord, true);
        }

        public RevisionRecordBase GetLastRevision()
        {
            if (Header.LastRevOffset > 0)
            {
                return GetRecord(CreateRevisionRecord, false, Header.LastRevOffset);
            }
            return null;
        }

        public RevisionRecordBase GetPreviousRevision(RevisionRecordBase revision)
        {
            if (revision.PrevRevOffset > 0)
            {
                return GetRecord(CreateRevisionRecord, false, revision.PrevRevOffset);
            }
            return null;
        }

        public SourceSafe.Physical.DeltaDiff.DeltaRecord GetPreviousDelta(SourceSafe.Physical.Revisions.EditRevisionRecord revision)
        {
            if (revision.PrevDeltaOffset > 0)
            {
                var record = new SourceSafe.Physical.DeltaDiff.DeltaRecord();
                ReadRecord(record, revision.PrevDeltaOffset);
                return record;
            }
            return null;
        }

        [System.Obsolete("Currently unused")]
        public ICollection<string> GetProjects()
        {
            // #TODO: change this to regular List then do a reverse for the result
            var result = new LinkedList<string>();
            if (Header is VssItemFileHeaderRecord fileHeader)
            {
                var record = new ProjectRecord();
                int offset = fileHeader.ProjectOffset;
                while (offset > 0)
                {
                    ReadRecord(record, offset);
                    if (!string.IsNullOrEmpty(record.ProjectFile))
                    {
                        result.AddFirst(record.ProjectFile);
                    }
                    offset = record.PrevProjectOffset;
                }
            }
            return result;
        }

        private static VssRecordBase CreateVssRecord(
            RecordHeader recordHeader,
            SourceSafe.IO.VssBufferReader recordReader)
        {
            VssRecordBase record = null;
            switch (recordHeader.Signature)
            {
                case RevisionRecordBase.SIGNATURE:
                    record = CreateRevisionRecord(recordHeader, recordReader);
                    break;
                case CommentRecord.SIGNATURE:
                    record = new CommentRecord();
                    break;
                case CheckoutRecord.SIGNATURE:
                    record = new CheckoutRecord();
                    break;
                case ProjectRecord.SIGNATURE:
                    record = new ProjectRecord();
                    break;
                case BranchRecord.SIGNATURE:
                    record = new BranchRecord();
                    break;
                case SourceSafe.Physical.DeltaDiff.DeltaRecord.SIGNATURE:
                    record = new SourceSafe.Physical.DeltaDiff.DeltaRecord();
                    break;
            }
            return record;
        }

        private static RevisionRecordBase CreateRevisionRecord(
            RecordHeader recordHeader,
            SourceSafe.IO.VssBufferReader recordReader)
        {
            if (recordHeader.Signature != RevisionRecordBase.SIGNATURE)
            {
                return null;
            }

            RevisionRecordBase record;
            RevisionAction action = RevisionRecordBase.PeekAction(recordReader);
            switch (action)
            {
                case RevisionAction.Label:
                    record = new LabelRevisionRecord();
                    break;
                case RevisionAction.DestroyProject:
                case RevisionAction.DestroyFile:
                    record = new DestroyRevisionRecord();
                    break;
                case RevisionAction.RenameProject:
                case RevisionAction.RenameFile:
                    record = new RenameRevisionRecord();
                    break;
                case RevisionAction.MoveFrom:
                case RevisionAction.MoveTo:
                    record = new MoveRevisionRecord();
                    break;
                case RevisionAction.ShareFile:
                    record = new ShareRevisionRecord();
                    break;
                case RevisionAction.BranchFile:
                case RevisionAction.CreateBranch:
                    record = new BranchRevisionRecord();
                    break;
                case RevisionAction.EditFile:
                    record = new EditRevisionRecord();
                    break;
                case RevisionAction.ArchiveProject:
                case RevisionAction.RestoreProject:
                case RevisionAction.RestoreFile:
                    record = new ArchiveRevisionRecord();
                    break;
                case RevisionAction.CreateProject:
                case RevisionAction.AddProject:
                case RevisionAction.AddFile:
                case RevisionAction.DeleteProject:
                case RevisionAction.DeleteFile:
                case RevisionAction.RecoverProject:
                case RevisionAction.RecoverFile:
                case RevisionAction.CreateFile:
                default:
                    record = new CommonRevisionRecord();
                    break;

                case RevisionAction.CheckInProject:
                case RevisionAction.ArchiveVersionFile:
                case RevisionAction.RestoreVersionFile:
                case RevisionAction.PinFile:
                case RevisionAction.UnpinFile:
                    // Untested actions, from #vssnotes
                    goto default;
            }
            return record;
        }
    }
}
