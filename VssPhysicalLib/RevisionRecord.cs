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
using System.IO;
using SourceSafe.Physical.Records;

namespace Hpdi.VssPhysicalLib
{
    /// <summary>
    /// Enumeration of physical VSS revision actions.
    /// </summary>
    /// <author>Trevor Robinson</author>
    public enum Action
    {
        // project actions
        Label = 0,
        CreateProject = 1,
        AddProject = 2,
        AddFile = 3,
        DestroyProject = 4,
        DestroyFile = 5,
        DeleteProject = 6,
        DeleteFile = 7,
        RecoverProject = 8,
        RecoverFile = 9,
        RenameProject = 10,
        RenameFile = 11,
        MoveFrom = 12,
        MoveTo = 13,
        ShareFile = 14,
        BranchFile = 15,

        // file actions
        CreateFile = 16,
        EditFile = 17,
        CheckInProject = 18, // Untested, from #vssnotes
        CreateBranch = 19, // #vssnotes says this is RollBack?

        ArchiveVersionFile = 20, // Untested, from #vssnotes
        RestoreVersionFile = 21, // Untested, from #vssnotes
        ArchiveFile = 22, // Untested, from #vssnotes

        // archive actions
        ArchiveProject = 23,
        RestoreFile = 24,
        RestoreProject = 25,

        PinFile = 26, // Untested, from #vssnotes
        UnpinFile = 27, // Untested, from #vssnotes
    }

    /// <summary>
    /// VSS record representing a project/file revision.
    /// </summary>
    /// <author>Trevor Robinson</author>
    /// <seealso cref="VssScanLogEntry"/>
    public class RevisionRecord : VssRecordBase
    {
        public const string SIGNATURE = "EL";
        public override string Signature => SIGNATURE;

        /// <summary>
        /// This field indicates the absolute file offset of the previous log
        /// entry chunk in the file. Scanning the log of a file generally runs
        /// backwards in time. Start with the last log entry in the file, and
        /// use m_PreviousOffset to seek to the start of the preceding chunk.
        /// </summary>
        public int PrevRevOffset { get; private set; }
        public Action Action { get; private set; }
        /// <summary>
        /// Each log entry is tagged with an incrementing version number.
        /// The first entry starts at 1, and each subsequent entry increments.
        /// This is the version number value that VSS displays when showing the
        /// log of a file.
        /// </summary>
        public int Revision { get; private set; }
        /// <remarks>
        /// From VssScanLog:
        ///     Also note: on my system, I have to use gmtime() to recover the
        ///     correct local time at which an operation occurred.  This implies
        ///     that all timestamps are in local time, not GMT, which could cause
        ///     problems when accessing VSS from machines that are in different
        ///     time zones.
        /// </remarks>
        public DateTime DateTime { get; private set; }
        /// <summary>
        /// Name of user who performed the operation.
        /// </summary>
        public string User { get; private set; }
        /// <summary>
        /// This is only used for VssOpcode_Labeled operations. It will contain
        /// the label assigned to this file.
        ///
        /// Note that a label is only applied to the selected file or directory.
        /// VSS will logically display that label when showing the change log of
        /// child files/directories, but the label itself is not written into
        /// any other files. The exception is the "data\labels" directory,
        /// which contains a file for every label ever created. This appears to
        /// be the information that is used when VSS shows labels in the history
        /// dialog. These small text files contain the path of the file that was
        /// tagged, and a timestamp (this timestamp is a 32-bit time_t value, the
        /// same as <see cref="DateTime"/>).
        /// </summary>
        public string Label { get; private set; }
        /// <summary>
        /// Absolute offset at which the comment chunk is located.  Most types of
        /// log entries will require a comment. Even if the user did not type
        /// one in, it will still create a comment chunk. This field stores the
        /// offset of the comment.
        ///
        /// Note: If a user edited a comment at a later date, this will not modify
        /// the existing comment chunk. Instead, a new comment chunk will be
        /// appended to the end of the file, and <see cref="CommentOffset"/> will be updated
        /// to point to the new comment chunk.
        ///
        /// In the few cases where there is no comment, this will be the offset of
        /// the next chunk in the file. However, since this value will be modified
        /// when editing comments, you cannot rely upon this value to point to the
        /// start of the next chunk.
        ///
        /// This field is only meaningful if <see cref="CommentLength"/> is non-zero.
        /// </summary>
        public int CommentOffset { get; private set; }
        /// <summary>
        /// A label operation is always followed by a comment chunk. These two
        /// fields indicate the position of the comment chunk for the label.
        /// This comment chunk will normally occur immediately following the
        /// label operation. However, if someone edited the comment at a later
        /// time, the offset will be that of the edited comment.
        ///
        /// Label comments are separate from regular comments, since a label may
        /// have both types of comments. For non-label operations, these fields
        /// appear to always be zero.
        /// </summary>
        public int LabelCommentOffset { get; private set; }
        /// <summary>
        /// The length of the comment contained in the chunk referenced by the
        /// <see cref="CommentOffset"/> field. If <see cref="CommentLength"/> is zero, then there is no
        /// comment. However, most operations do require a comment, so a comment
        /// chunk will always exist for them. If the user did not type in a
        /// comment when checking in the file, a 1-byte comment will be created
        /// that contains the string "\0".
        ///
        /// This length appears to always include the '\0' terminator at the end
        /// of the comment chunk.
        /// </summary>
        public int CommentLength { get; private set; }
        public int LabelCommentLength { get; private set; }

        public static Action PeekAction(SourceSafe.IO.VssBufferReader reader)
        {
            int saveOffset = reader.Offset;
            try
            {
                reader.Skip(4);
                return (Action)reader.ReadInt16();
            }
            finally
            {
                reader.Offset = saveOffset;
            }
        }

        public override void Read(SourceSafe.IO.VssBufferReader reader, RecordHeader header)
        {
            base.Read(reader, header);

            PrevRevOffset = reader.ReadInt32();
            Action = (Action)reader.ReadInt16();
            Revision = reader.ReadInt16();
            DateTime = reader.ReadDateTime();
            User = reader.ReadString(32);
            Label = reader.ReadString(32);
            CommentOffset = reader.ReadInt32();
            LabelCommentOffset = reader.ReadInt32();
            CommentLength = reader.ReadInt16();
            LabelCommentLength = reader.ReadInt16();
        }

        public override void Dump(TextWriter writer, int indent)
        {
            string indentStr = SourceSafe.IO.OutputUtil.GetIndentString(indent);

            writer.Write(indentStr);
            writer.WriteLine("Prev rev offset: {0:X6}", PrevRevOffset);
            writer.Write(indentStr);
            writer.WriteLine("#{0:D3} {1} by '{2}' at {3}",
                Revision, Action, User, DateTime);
            writer.Write(indentStr);
            writer.WriteLine("Label: {0}", Label);
            writer.Write(indentStr);
            writer.WriteLine("Comment: length {0}, offset {1:X6}", CommentLength, CommentOffset);
            writer.Write(indentStr);
            writer.WriteLine("Label comment: length {0}, offset {1:X6}", LabelCommentLength, LabelCommentOffset);
        }
    }

    public sealed class CommonRevisionRecord : RevisionRecord
    {
        public SourceSafe.Physical.VssName Name { get; private set; }
        public string Physical { get; private set; }

        public override void Read(SourceSafe.IO.VssBufferReader reader, RecordHeader header)
        {
            base.Read(reader, header);

            Name = reader.ReadName();
            Physical = reader.ReadString(10);
        }

        public override void Dump(TextWriter writer, int indent)
        {
            base.Dump(writer, indent);
            string indentStr = SourceSafe.IO.OutputUtil.GetIndentString(indent);

            writer.Write(indentStr);
            writer.WriteLine($"Name: {Name.ShortName} ({Physical})");
        }
    }

    public sealed class DestroyRevisionRecord : RevisionRecord
    {
        short unkShort;

        public SourceSafe.Physical.VssName Name { get; private set; }
        public string Physical { get; private set; }

        public override void Read(SourceSafe.IO.VssBufferReader reader, RecordHeader header)
        {
            base.Read(reader, header);

            Name = reader.ReadName();
            unkShort = reader.ReadInt16(); // 0 or 1
            Physical = reader.ReadString(10);
        }

        public override void Dump(TextWriter writer, int indent)
        {
            base.Dump(writer, indent);
            string indentStr = SourceSafe.IO.OutputUtil.GetIndentString(indent);

            writer.Write(indentStr);
            writer.WriteLine($"Name: {Name.ShortName} ({Physical})");
            if (unkShort != 0)
            {
                writer.Write(SourceSafe.IO.OutputUtil.GetIndentString(indent + 1));
                writer.WriteLine($"Unknown: {unkShort}");
            }
        }
    }

    public sealed class RenameRevisionRecord : RevisionRecord
    {
        public SourceSafe.Physical.VssName Name { get; private set; }
        public SourceSafe.Physical.VssName OldName { get; private set; }
        public string Physical { get; private set; }

        public override void Read(SourceSafe.IO.VssBufferReader reader, RecordHeader header)
        {
            base.Read(reader, header);

            Name = reader.ReadName();
            OldName = reader.ReadName();
            Physical = reader.ReadString(10);
        }

        public override void Dump(TextWriter writer, int indent)
        {
            base.Dump(writer, indent);
            string indentStr = SourceSafe.IO.OutputUtil.GetIndentString(indent);

            writer.Write(indentStr);
            writer.WriteLine($"Name: {OldName.ShortName} -> {Name.ShortName} ({Physical})");
        }
    }

    public sealed class MoveRevisionRecord : RevisionRecord
    {
        public string ProjectPath { get; private set; }
        public SourceSafe.Physical.VssName Name { get; private set; }
        public string Physical { get; private set; }

        public override void Read(SourceSafe.IO.VssBufferReader reader, RecordHeader header)
        {
            base.Read(reader, header);

            ProjectPath = reader.ReadString(260);
            Name = reader.ReadName();
            Physical = reader.ReadString(10);
        }

        public override void Dump(TextWriter writer, int indent)
        {
            base.Dump(writer, indent);
            string indentStr = SourceSafe.IO.OutputUtil.GetIndentString(indent);

            writer.Write(indentStr);
            writer.WriteLine($"Project path: {ProjectPath}");
            writer.Write(indentStr);
            writer.WriteLine($"Name: {Name.ShortName} ({Physical})");
        }
    }

    public sealed class ShareRevisionRecord : RevisionRecord
    {
        short unkShort;

        /// <summary>
        /// This field is used for VssOpcode_SharedFile and VssOpcode_CheckedIn
        /// operations. Note that this is a path within the VSS database, and
        /// will start with "$/...". Any path that starts with "$" will be a
        /// reference to a project or file within the database.
        ///
        /// For VssOpcode_SharedFile, this contains the path of the file being
        /// shared.
        ///
        /// For VssOpcode_CheckedIn, this is the path within the database from
        /// which the check-in was performed. This is really only relevant when
        /// a file is shared between multiple projects. <see cref="ProjectPath"/> will
        /// indicate the project from which the check-in was performed.
        /// It's not clear how this is useful, except perhaps for change auditing.
        /// Within VSS itself, this information does not appear to be used for
        /// anything.
        /// </summary>
        public string ProjectPath { get; private set; }
        public SourceSafe.Physical.VssName Name { get; private set; }
        public short UnpinnedRevision { get; private set; }
        public short PinnedRevision { get; private set; }
        public string Physical { get; private set; }

        public override void Read(SourceSafe.IO.VssBufferReader reader, RecordHeader header)
        {
            base.Read(reader, header);

            ProjectPath = reader.ReadString(260);
            Name = reader.ReadName();
            UnpinnedRevision = reader.ReadInt16();
            PinnedRevision = reader.ReadInt16();
            unkShort = reader.ReadInt16(); // often seems to increment
            Physical = reader.ReadString(10);
        }

        public override void Dump(TextWriter writer, int indent)
        {
            base.Dump(writer, indent);
            string indentStr = SourceSafe.IO.OutputUtil.GetIndentString(indent);

            writer.Write(indentStr);
            writer.WriteLine($"Project path: {ProjectPath}");
            writer.Write(indentStr);
            writer.WriteLine($"Name: {Name.ShortName} ({Physical})");
            if (UnpinnedRevision == 0)
            {
                writer.Write(indentStr);
                writer.WriteLine($"Pinned at revision {PinnedRevision}");
            }
            else if (UnpinnedRevision > 0)
            {
                writer.Write(indentStr);
                writer.WriteLine($"Unpinned at revision {UnpinnedRevision}");
            }

            if (unkShort != 0)
            {
                writer.Write(SourceSafe.IO.OutputUtil.GetIndentString(indent + 1));
                writer.WriteLine($"Unknown: {unkShort}");
            }
        }
    }

    public sealed class BranchRevisionRecord : RevisionRecord
    {
        public SourceSafe.Physical.VssName Name { get; private set; }
        public string Physical { get; private set; }
        public string BranchFile { get; private set; }

        public override void Read(SourceSafe.IO.VssBufferReader reader, RecordHeader header)
        {
            base.Read(reader, header);

            Name = reader.ReadName();
            Physical = reader.ReadString(10);
            BranchFile = reader.ReadString(10);
        }

        public override void Dump(TextWriter writer, int indent)
        {
            base.Dump(writer, indent);
            string indentStr = SourceSafe.IO.OutputUtil.GetIndentString(indent);

            writer.Write(indentStr);
            writer.WriteLine($"Name: {Name.ShortName} ({Physical})");
            writer.Write(indentStr);
            writer.WriteLine($"Branched from file: {BranchFile}");
        }
    }

    public class EditRevisionRecord : RevisionRecord
    {
        public int PrevDeltaOffset { get; private set; }
        public int Unknown5C { get; private set; }
        public string ProjectPath { get; private set; }

        public static bool ReadCheckForNonZeroUnknown5C { get; set; } = false;
        public override void Read(SourceSafe.IO.VssBufferReader reader, RecordHeader header)
        {
            base.Read(reader, header);

            PrevDeltaOffset = reader.ReadInt32();
            Unknown5C = reader.ReadInt32();
#if DEBUG
            if (ReadCheckForNonZeroUnknown5C && Unknown5C != 0)
            {
                "".ToString(); // place a breakpoint as needed
            }
#endif // DEBUG
            ProjectPath = reader.ReadString(260);
        }

        public override void Dump(TextWriter writer, int indent)
        {
            base.Dump(writer, indent);
            string indentStr = SourceSafe.IO.OutputUtil.GetIndentString(indent);

            writer.Write(indentStr);
            writer.WriteLine("Prev delta offset: {0:X6}", PrevDeltaOffset);
            if (Unknown5C != 0)
            {
                writer.Write(indentStr);
                writer.WriteLine("Unknown delta offset: {0:X8}", Unknown5C);
            }
            writer.Write(indentStr);
            writer.WriteLine("Project path: {0}", ProjectPath);
        }
    }

    public sealed class ArchiveRevisionRecord : RevisionRecord
    {
        public SourceSafe.Physical.VssName Name { get; private set; }
        public string Physical { get; private set; }
        public string ArchivePath { get; private set; }

        public override void Read(SourceSafe.IO.VssBufferReader reader, RecordHeader header)
        {
            base.Read(reader, header);

            Name = reader.ReadName();
            Physical = reader.ReadString(10);
            reader.SkipUnknown(2); // 0?
            ArchivePath = reader.ReadString(260);
            reader.SkipUnknown(4); // ?
        }

        public override void Dump(TextWriter writer, int indent)
        {
            base.Dump(writer, indent);
            string indentStr = SourceSafe.IO.OutputUtil.GetIndentString(indent);

            writer.Write(indentStr);
            writer.WriteLine("Name: {0} ({1})", Name.ShortName, Physical);
            writer.Write(indentStr);
            writer.WriteLine("Archive path: {0}", ArchivePath);
        }
    }
}
