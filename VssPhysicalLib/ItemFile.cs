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

using System.Collections.Generic;
using System.Text;

namespace Hpdi.VssPhysicalLib
{
    /// <summary>
    /// Represents a file containing VSS project/file records.
    /// </summary>
    /// <author>Trevor Robinson</author>
    public sealed class ItemFile : VssRecordFile
    {
        const string FILE_SIGNATUIRE = "SourceSafe@Microsoft";

        public ItemHeaderRecord Header { get; }

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

                var fileType = (ItemType)reader.ReadInt16();
                short fileVersion = reader.ReadInt16();
                if (fileVersion != 6)
                {
                    throw new BadHeaderException($"Incorrect file version: {fileVersion}");
                }

                reader.Skip(16); // reserved; always 0

                if (fileType == ItemType.Project)
                {
                    Header = new ProjectHeaderRecord();
                }
                else
                {
                    Header = new FileHeaderRecord();
                }

                ReadRecord(Header);
                if (Header.ItemType != fileType)
                {
                    throw new BadHeaderException("Header record type mismatch");
                }
            }
            catch (EndOfBufferException e)
            {
                throw new BadHeaderException("Truncated header", e);
            }
        }

        public VssRecord GetRecord(int offset)
        {
            return GetRecord<VssRecord>(CreateRecord, false, offset);
        }

        public VssRecord GetNextRecord(bool skipUnknown)
        {
            #if false   // #REVIEW: https://github.com/trevorr/vss2git/pull/48#issuecomment-804139011
                        // "When you change a comment of a checkin, VSS does not change the original comment record, but appends a new comment record to the physical file,
                        // updating the EOF offset. Therefore the proposed fix let Vss2Git fail to analyse any physical file with a such edited comment, because the iterator
                        // skips all the records between the one owning the new comment and the corresponding comment record, while building the changeset list."
            if (reader.Offset == this.Header.EofOffset)
            {
                return null;
            }
            #endif
            return GetNextRecord<VssRecord>(CreateRecord, skipUnknown);
        }

        public RevisionRecord GetFirstRevision()
        {
            if (Header.FirstRevOffset > 0)
            {
                return GetRecord<RevisionRecord>(CreateRevisionRecord, false, Header.FirstRevOffset);
            }
            return null;
        }

        public RevisionRecord GetNextRevision(RevisionRecord revision)
        {
            #if false   // #REVIEW: https://github.com/trevorr/vss2git/pull/48#issuecomment-804139011
                        // "When you change a comment of a checkin, VSS does not change the original comment record, but appends a new comment record to the physical file,
                        // updating the EOF offset. Therefore the proposed fix let Vss2Git fail to analyse any physical file with a such edited comment, because the iterator
                        // skips all the records between the one owning the new comment and the corresponding comment record, while building the changeset list."
            if (reader.Offset == this.Header.EofOffset)
            {
                return null;
            }
            #endif
            reader.Offset = revision.Header.Offset + revision.Header.Length + RecordHeader.LENGTH;
            return GetNextRecord<RevisionRecord>(CreateRevisionRecord, true);
        }

        public RevisionRecord GetLastRevision()
        {
            if (Header.LastRevOffset > 0)
            {
                return GetRecord<RevisionRecord>(CreateRevisionRecord, false, Header.LastRevOffset);
            }
            return null;
        }

        public RevisionRecord GetPreviousRevision(RevisionRecord revision)
        {
            if (revision.PrevRevOffset > 0)
            {
                return GetRecord<RevisionRecord>(CreateRevisionRecord, false, revision.PrevRevOffset);
            }
            return null;
        }

        public DeltaRecord GetPreviousDelta(EditRevisionRecord revision)
        {
            if (revision.PrevDeltaOffset > 0)
            {
                var record = new DeltaRecord();
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
            if (Header is FileHeaderRecord fileHeader)
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

        private static VssRecord CreateRecord(
            RecordHeader recordHeader, BufferReader recordReader)
        {
            VssRecord record = null;
            switch (recordHeader.Signature)
            {
                case RevisionRecord.SIGNATURE:
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
                case DeltaRecord.SIGNATURE:
                    record = new DeltaRecord();
                    break;
            }
            return record;
        }

        private static RevisionRecord CreateRevisionRecord(
            RecordHeader recordHeader, BufferReader recordReader)
        {
            if (recordHeader.Signature != RevisionRecord.SIGNATURE)
            {
                return null;
            }

            RevisionRecord record;
            Action action = RevisionRecord.PeekAction(recordReader);
            switch (action)
            {
                case Action.Label:
                    record = new RevisionRecord();
                    break;
                case Action.DestroyProject:
                case Action.DestroyFile:
                    record = new DestroyRevisionRecord();
                    break;
                case Action.RenameProject:
                case Action.RenameFile:
                    record = new RenameRevisionRecord();
                    break;
                case Action.MoveFrom:
                case Action.MoveTo:
                    record = new MoveRevisionRecord();
                    break;
                case Action.ShareFile:
                    record = new ShareRevisionRecord();
                    break;
                case Action.BranchFile:
                case Action.CreateBranch:
                    record = new BranchRevisionRecord();
                    break;
                case Action.EditFile:
                    record = new EditRevisionRecord();
                    break;
                case Action.ArchiveProject:
                case Action.RestoreProject:
                case Action.RestoreFile:
                    record = new ArchiveRevisionRecord();
                    break;
                case Action.CreateProject:
                case Action.AddProject:
                case Action.AddFile:
                case Action.DeleteProject:
                case Action.DeleteFile:
                case Action.RecoverProject:
                case Action.RecoverFile:
                case Action.CreateFile:
                default:
                    record = new CommonRevisionRecord();
                    break;

                case Action.CheckInProject:
                case Action.ArchiveVersionFile:
                case Action.RestoreVersionFile:
                case Action.PinFile:
                case Action.UnpinFile:
                    // Untested actions, from #vssnotes
                    goto default;
            }
            return record;
        }
    }
}
