
// https://github.com/trevorr/vss2git/pull/48#issuecomment-804139011
//      "When you change a comment of a checkin, VSS does not change the original comment record, but appends a new comment record to the physical file,
//      updating the EOF offset. Therefore the proposed fix let Vss2Git fail to analyse any physical file with a such edited comment, because the iterator
//      skips all the records between the one owning the new comment and the corresponding comment record, while building the changeset list."
// I contend that the EOF offset is valid, contrary to what this Nyk72's comment says.
#define EOF_OFFSET_CHECK_ENABLED
//
#define LAST_REVISION_OFFSET_CHECK_ENABLED

using SourceSafe.Physical.Revisions;

namespace SourceSafe.Physical.Files
{
    /// <summary>
    /// Represents a file containing VSS project/file records.
    /// </summary>
    internal sealed class VssPhysicalFile : VssRecordFileBase
    {
        const string FILE_SIGNATUIRE = "SourceSafe@Microsoft";

        public Records.VssItemHeaderRecordBase Header { get; }

        public VssPhysicalFile(string filename, System.Text.Encoding encoding)
            : base(filename, encoding)
        {
            try
            {
                string fileSig = reader.ReadString(0x20);
                if (fileSig != FILE_SIGNATUIRE)
                {
                    throw new Records.BadHeaderException("Incorrect file signature");
                }

                var fileType = (VssItemType)reader.ReadInt16();
                short fileVersion = reader.ReadInt16();
                if (fileVersion != 6)
                {
                    throw new Records.BadHeaderException($"Incorrect file version: {fileVersion}");
                }

                reader.SkipAssumedToBeAllZeros(16); // reserved; always 0

                if (fileType == VssItemType.Project)
                {
                    Header = new Records.VssItemProjectHeaderRecord();
                }
                else
                {
                    Header = new Records.VssItemFileHeaderRecord();
                }

                ReadRecord(Header);
                if (Header.ItemType != fileType)
                {
                    throw new Records.BadHeaderException("Header record type mismatch");
                }
            }
            catch (IO.EndOfBufferException e)
            {
                throw new Records.BadHeaderException("Truncated header", e);
            }
        }

        [Obsolete("Unused")]
        public Records.VssRecordBase? GetRecordByFileOffset(int fileOffset)
        {
            return GetRecord(CreateVssRecord, false, fileOffset);
        }

        public Records.VssRecordBase? GetNextRecord(bool skipUnknown)
        {
#if EOF_OFFSET_CHECK_ENABLED
            if (reader.Offset == Header.EofOffset)
            {
                return null;
            }
#endif // EOF_OFFSET_CHECK_ENABLED

#if LAST_REVISION_OFFSET_CHECK_ENABLED
            if (reader.Offset > Header.LastRevOffset)
            {
                return null;
            }
#endif // LAST_REVISION_OFFSET_CHECK_ENABLED

            return GetNextRecord(CreateVssRecord, skipUnknown);
        }

        public RevisionRecordBase? GetFirstRevision()
        {
            if (Header.FirstRevOffset > 0)
            {
                return GetRecord(CreateRevisionRecord, false, Header.FirstRevOffset);
            }
            return null;
        }

        public RevisionRecordBase? GetNextRevision(RevisionRecordBase revision)
        {
#if EOF_OFFSET_CHECK_ENABLED
            if (reader.Offset == Header.EofOffset)
            {
                return null;
            }
#endif // EOF_OFFSET_CHECK_ENABLED

#if LAST_REVISION_OFFSET_CHECK_ENABLED
            if (reader.Offset > Header.LastRevOffset)
            {
                return null;
            }
#endif // LAST_REVISION_OFFSET_CHECK_ENABLED

            reader.Offset = revision.Header.Offset + revision.Header.Length + Records.RecordHeader.LENGTH;
            return GetNextRecord(CreateRevisionRecord, true);
        }

        public RevisionRecordBase? GetLastRevision()
        {
            if (Header.LastRevOffset > 0)
            {
                return GetRecord(CreateRevisionRecord, false, Header.LastRevOffset);
            }
            return null;
        }

        public RevisionRecordBase? GetPreviousRevision(RevisionRecordBase revision)
        {
            if (revision.PrevRevOffset > 0)
            {
                return GetRecord(CreateRevisionRecord, false, revision.PrevRevOffset);
            }
            return null;
        }

        public DeltaDiff.DeltaRecord? GetPreviousDelta(EditRevisionRecord revision)
        {
            if (revision.PrevDeltaOffset > 0)
            {
                var record = new DeltaDiff.DeltaRecord();
                ReadRecord(record, revision.PrevDeltaOffset);
                return record;
            }
            return null;
        }

        [Obsolete("Unused")]
        public ICollection<string> GetProjectFileNames()
        {
            var result = new List<string>();
            if (Header is Records.VssItemFileHeaderRecord fileHeader)
            {
                var record = new Records.ProjectRecord();
                int offset = fileHeader.ProjectOffset;
                while (offset > 0)
                {
                    ReadRecord(record, offset);
                    if (!string.IsNullOrEmpty(record.ProjectFile))
                    {
                        result.Add(record.ProjectFile);
                    }
                    offset = record.PrevProjectOffset;
                }

                // This used to be a LinkedList with AddFirst calls, so we need to reverse the result
                result.Reverse();
            }
            return result;
        }

        private static Records.VssRecordBase? CreateVssRecord(
            Records.RecordHeader recordHeader,
            IO.VssBufferReader recordReader)
        {
            Records.VssRecordBase? record = null;
            switch (recordHeader.Signature)
            {
                case RevisionRecordBase.SIGNATURE:
                    record = CreateRevisionRecord(recordHeader, recordReader);
                    break;
                case Records.CommentRecord.SIGNATURE:
                    record = new Records.CommentRecord();
                    break;
                case Records.CheckoutRecord.SIGNATURE:
                    record = new Records.CheckoutRecord();
                    break;
                case Records.ProjectRecord.SIGNATURE:
                    record = new Records.ProjectRecord();
                    break;
                case Records.BranchRecord.SIGNATURE:
                    record = new Records.BranchRecord();
                    break;
                case DeltaDiff.DeltaRecord.SIGNATURE:
                    record = new DeltaDiff.DeltaRecord();
                    break;
            }
            return record;
        }

        private static RevisionRecordBase? CreateRevisionRecord(
            Records.RecordHeader recordHeader,
            IO.VssBufferReader recordReader)
        {
            if (recordHeader.Signature != RevisionRecordBase.SIGNATURE)
            {
                return null;
            }

            RevisionRecordBase? record;
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
    };
}
