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
    public class RevisionRecord : VssRecord
    {
        public const string SIGNATURE = "EL";

        public override string Signature => SIGNATURE;
        public int PrevRevOffset { get; private set; }
        public Action Action { get; private set; }
        public int Revision { get; private set; }
        public DateTime DateTime { get; private set; }
        public string User { get; private set; }
        public string Label { get; private set; }
        public int CommentOffset { get; private set; }
        public int LabelCommentOffset { get; private set; }
        public int CommentLength { get; private set; }
        public int LabelCommentLength { get; private set; }

        public static Action PeekAction(BufferReader reader)
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

        public override void Read(BufferReader reader, RecordHeader header)
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
            string indentStr = DumpGetIndentString(indent);

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
        public VssName Name { get; private set; }
        public string Physical { get; private set; }

        public override void Read(BufferReader reader, RecordHeader header)
        {
            base.Read(reader, header);

            Name = reader.ReadName();
            Physical = reader.ReadString(10);
        }

        public override void Dump(TextWriter writer, int indent)
        {
            base.Dump(writer, indent);
            string indentStr = DumpGetIndentString(indent);

            writer.Write(indentStr);
            writer.WriteLine("Name: {0} ({1})", Name.ShortName, Physical);
        }
    }

    public sealed class DestroyRevisionRecord : RevisionRecord
    {
        short unkShort;

        public VssName Name { get; private set; }
        public string Physical { get; private set; }

        public override void Read(BufferReader reader, RecordHeader header)
        {
            base.Read(reader, header);

            Name = reader.ReadName();
            unkShort = reader.ReadInt16(); // 0 or 1
            Physical = reader.ReadString(10);
        }

        public override void Dump(TextWriter writer, int indent)
        {
            base.Dump(writer, indent);
            string indentStr = DumpGetIndentString(indent);

            writer.Write(indentStr);
            writer.WriteLine("Name: {0} ({1})", Name.ShortName, Physical);
            if (unkShort != 0)
            {
                writer.Write(DumpGetIndentString(indent + 1));
                writer.WriteLine("Unknown: {0}", unkShort);
            }
        }
    }

    public sealed class RenameRevisionRecord : RevisionRecord
    {
        public VssName Name { get; private set; }
        public VssName OldName { get; private set; }
        public string Physical { get; private set; }

        public override void Read(BufferReader reader, RecordHeader header)
        {
            base.Read(reader, header);

            Name = reader.ReadName();
            OldName = reader.ReadName();
            Physical = reader.ReadString(10);
        }

        public override void Dump(TextWriter writer, int indent)
        {
            base.Dump(writer, indent);
            string indentStr = DumpGetIndentString(indent);

            writer.Write(indentStr);
            writer.WriteLine("Name: {0} -> {1} ({2})",
                OldName.ShortName, Name.ShortName, Physical);
        }
    }

    public sealed class MoveRevisionRecord : RevisionRecord
    {
        public string ProjectPath { get; private set; }
        public VssName Name { get; private set; }
        public string Physical { get; private set; }

        public override void Read(BufferReader reader, RecordHeader header)
        {
            base.Read(reader, header);

            ProjectPath = reader.ReadString(260);
            Name = reader.ReadName();
            Physical = reader.ReadString(10);
        }

        public override void Dump(TextWriter writer, int indent)
        {
            base.Dump(writer, indent);
            string indentStr = DumpGetIndentString(indent);

            writer.Write(indentStr);
            writer.WriteLine("Project path: {0}", ProjectPath);
            writer.Write(indentStr);
            writer.WriteLine("Name: {0} ({1})", Name.ShortName, Physical);
        }
    }

    public sealed class ShareRevisionRecord : RevisionRecord
    {
        short unkShort;

        public string ProjectPath { get; private set; }
        public VssName Name { get; private set; }
        public short UnpinnedRevision { get; private set; }
        public short PinnedRevision { get; private set; }
        public string Physical { get; private set; }

        public override void Read(BufferReader reader, RecordHeader header)
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
            string indentStr = DumpGetIndentString(indent);

            writer.Write(indentStr);
            writer.WriteLine("Project path: {0}", ProjectPath);
            writer.Write(indentStr);
            writer.WriteLine("Name: {0} ({1})", Name.ShortName, Physical);
            if (UnpinnedRevision == 0)
            {
                writer.Write(indentStr);
                writer.WriteLine("Pinned at revision {0}", PinnedRevision);
            }
            else if (UnpinnedRevision > 0)
            {
                writer.Write(indentStr);
                writer.WriteLine("Unpinned at revision {0}", UnpinnedRevision);
            }

            if (unkShort != 0)
            {
                writer.Write(DumpGetIndentString(indent + 1));
                writer.WriteLine("Unknown: {0}", unkShort);
            }
        }
    }

    public sealed class BranchRevisionRecord : RevisionRecord
    {
        public VssName Name { get; private set; }
        public string Physical { get; private set; }
        public string BranchFile { get; private set; }

        public override void Read(BufferReader reader, RecordHeader header)
        {
            base.Read(reader, header);

            Name = reader.ReadName();
            Physical = reader.ReadString(10);
            BranchFile = reader.ReadString(10);
        }

        public override void Dump(TextWriter writer, int indent)
        {
            base.Dump(writer, indent);
            string indentStr = DumpGetIndentString(indent);

            writer.Write(indentStr);
            writer.WriteLine("Name: {0} ({1})", Name.ShortName, Physical);
            writer.Write(indentStr);
            writer.WriteLine("Branched from file: {0}", BranchFile);
        }
    }

    public class EditRevisionRecord : RevisionRecord
    {
        public int PrevDeltaOffset { get; private set; }
        public int Unknown5C { get; private set; }
        public string ProjectPath { get; private set; }

        public static bool ReadCheckForNonZeroUnknown5C { get; set; } = false;
        public override void Read(BufferReader reader, RecordHeader header)
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
            string indentStr = DumpGetIndentString(indent);

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
        public VssName Name { get; private set; }
        public string Physical { get; private set; }
        public string ArchivePath { get; private set; }

        public override void Read(BufferReader reader, RecordHeader header)
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
            string indentStr = DumpGetIndentString(indent);

            writer.Write(indentStr);
            writer.WriteLine("Name: {0} ({1})", Name.ShortName, Physical);
            writer.Write(indentStr);
            writer.WriteLine("Archive path: {0}", ArchivePath);
        }
    }
}
