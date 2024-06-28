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

        protected int prevRevOffset;
        protected Action action;
        protected int revision;
        protected DateTime dateTime;
        protected string user;
        protected string label;
        protected int commentOffset; // or next revision if no comment
        protected int labelCommentOffset; // or label comment
        protected int commentLength;
        protected int labelCommentLength;

        public override string Signature { get { return SIGNATURE; } }
        public int PrevRevOffset { get { return prevRevOffset; } }
        public Action Action { get { return action; } }
        public int Revision { get { return revision; } }
        public DateTime DateTime { get { return dateTime; } }
        public string User { get { return user; } }
        public string Label { get { return label; } }
        public int CommentOffset { get { return commentOffset; } }
        public int LabelCommentOffset { get { return labelCommentOffset; } }
        public int CommentLength { get { return commentLength; } }
        public int LabelCommentLength { get { return labelCommentLength; } }

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

            prevRevOffset = reader.ReadInt32();
            action = (Action)reader.ReadInt16();
            revision = reader.ReadInt16();
            dateTime = reader.ReadDateTime();
            user = reader.ReadString(32);
            label = reader.ReadString(32);
            commentOffset = reader.ReadInt32();
            labelCommentOffset = reader.ReadInt32();
            commentLength = reader.ReadInt16();
            labelCommentLength = reader.ReadInt16();
        }

        public override void Dump(TextWriter writer, int indent)
        {
            string indentStr = DumpGetIndentString(indent);

            writer.Write(indentStr);
            writer.WriteLine("Prev rev offset: {0:X6}", prevRevOffset);
            writer.Write(indentStr);
            writer.WriteLine("#{0:D3} {1} by '{2}' at {3}",
                revision, action, user, dateTime);
            writer.Write(indentStr);
            writer.WriteLine("Label: {0}", label);
            writer.Write(indentStr);
            writer.WriteLine("Comment: length {0}, offset {1:X6}", commentLength, commentOffset);
            writer.Write(indentStr);
            writer.WriteLine("Label comment: length {0}, offset {1:X6}", labelCommentLength, labelCommentOffset);
        }
    }

    public class CommonRevisionRecord : RevisionRecord
    {
        VssName name;
        string physical;

        public VssName Name { get { return name; } }
        public string Physical { get { return physical; } }

        public override void Read(BufferReader reader, RecordHeader header)
        {
            base.Read(reader, header);

            name = reader.ReadName();
            physical = reader.ReadString(10);
        }

        public override void Dump(TextWriter writer, int indent)
        {
            base.Dump(writer, indent);
            string indentStr = DumpGetIndentString(indent);

            writer.Write(indentStr);
            writer.WriteLine("Name: {0} ({1})", name.ShortName, physical);
        }
    }

    public class DestroyRevisionRecord : RevisionRecord
    {
        VssName name;
        short unkShort;
        string physical;

        public VssName Name { get { return name; } }
        public string Physical { get { return physical; } }

        public override void Read(BufferReader reader, RecordHeader header)
        {
            base.Read(reader, header);

            name = reader.ReadName();
            unkShort = reader.ReadInt16(); // 0 or 1
            physical = reader.ReadString(10);
        }

        public override void Dump(TextWriter writer, int indent)
        {
            base.Dump(writer, indent);
            string indentStr = DumpGetIndentString(indent);

            writer.Write(indentStr);
            writer.WriteLine("Name: {0} ({1})", name.ShortName, physical);
        }
    }

    public class RenameRevisionRecord : RevisionRecord
    {
        VssName name;
        VssName oldName;
        string physical;

        public VssName Name { get { return name; } }
        public VssName OldName { get { return oldName; } }
        public string Physical { get { return physical; } }

        public override void Read(BufferReader reader, RecordHeader header)
        {
            base.Read(reader, header);

            name = reader.ReadName();
            oldName = reader.ReadName();
            physical = reader.ReadString(10);
        }

        public override void Dump(TextWriter writer, int indent)
        {
            base.Dump(writer, indent);
            string indentStr = DumpGetIndentString(indent);

            writer.Write(indentStr);
            writer.WriteLine("Name: {0} -> {1} ({2})",
                oldName.ShortName, name.ShortName, physical);
        }
    }

    public class MoveRevisionRecord : RevisionRecord
    {
        string projectPath;
        VssName name;
        string physical;

        public string ProjectPath { get { return projectPath; } }
        public VssName Name { get { return name; } }
        public string Physical { get { return physical; } }

        public override void Read(BufferReader reader, RecordHeader header)
        {
            base.Read(reader, header);

            projectPath = reader.ReadString(260);
            name = reader.ReadName();
            physical = reader.ReadString(10);
        }

        public override void Dump(TextWriter writer, int indent)
        {
            base.Dump(writer, indent);
            string indentStr = DumpGetIndentString(indent);

            writer.Write(indentStr);
            writer.WriteLine("Project path: {0}", projectPath);
            writer.Write(indentStr);
            writer.WriteLine("Name: {0} ({1})", name.ShortName, physical);
        }
    }

    public class ShareRevisionRecord : RevisionRecord
    {
        string projectPath;
        VssName name;
        short unpinnedRevision; // -1: shared, 0: pinned; >0 unpinned version
        short pinnedRevision; // >0: pinned version, ==0 unpinned
        short unkShort;
        string physical;

        public string ProjectPath { get { return projectPath; } }
        public VssName Name { get { return name; } }
        public short UnpinnedRevision { get { return unpinnedRevision; } }
        public short PinnedRevision { get { return pinnedRevision; } }
        public string Physical { get { return physical; } }

        public override void Read(BufferReader reader, RecordHeader header)
        {
            base.Read(reader, header);

            projectPath = reader.ReadString(260);
            name = reader.ReadName();
            unpinnedRevision = reader.ReadInt16();
            pinnedRevision = reader.ReadInt16();
            unkShort = reader.ReadInt16(); // often seems to increment
            physical = reader.ReadString(10);
        }

        public override void Dump(TextWriter writer, int indent)
        {
            base.Dump(writer, indent);
            string indentStr = DumpGetIndentString(indent);

            writer.Write(indentStr);
            writer.WriteLine("Project path: {0}", projectPath);
            writer.Write(indentStr);
            writer.WriteLine("Name: {0} ({1})", name.ShortName, physical);
            if (unpinnedRevision == 0)
            {
                writer.Write(indentStr);
                writer.WriteLine("Pinned at revision {0}", pinnedRevision);
            }
            else if (unpinnedRevision > 0)
            {
                writer.Write(indentStr);
                writer.WriteLine("Unpinned at revision {0}", unpinnedRevision);
            }
        }
    }

    public class BranchRevisionRecord : RevisionRecord
    {
        VssName name;
        string physical;
        string branchFile;

        public VssName Name { get { return name; } }
        public string Physical { get { return physical; } }
        public string BranchFile { get { return branchFile; } }

        public override void Read(BufferReader reader, RecordHeader header)
        {
            base.Read(reader, header);

            name = reader.ReadName();
            physical = reader.ReadString(10);
            branchFile = reader.ReadString(10);
        }

        public override void Dump(TextWriter writer, int indent)
        {
            base.Dump(writer, indent);
            string indentStr = DumpGetIndentString(indent);

            writer.Write(indentStr);
            writer.WriteLine("Name: {0} ({1})", name.ShortName, physical);
            writer.Write(indentStr);
            writer.WriteLine("Branched from file: {0}", branchFile);
        }
    }

    public class EditRevisionRecord : RevisionRecord
    {
        int prevDeltaOffset;
        string projectPath;

        public int PrevDeltaOffset { get { return prevDeltaOffset; } }
        public int Unknown5C { get; private set; }
        public string ProjectPath { get { return projectPath; } }

        public static bool ReadCheckForNonZeroUnknown5C { get; set; } = false;
        public override void Read(BufferReader reader, RecordHeader header)
        {
            base.Read(reader, header);

            prevDeltaOffset = reader.ReadInt32();
            Unknown5C = reader.ReadInt32();
#if DEBUG
            if (ReadCheckForNonZeroUnknown5C && Unknown5C != 0)
            {
                "".ToString(); // place a breakpoint as needed
            }
#endif // DEBUG
            projectPath = reader.ReadString(260);
        }

        public override void Dump(TextWriter writer, int indent)
        {
            base.Dump(writer, indent);
            string indentStr = DumpGetIndentString(indent);

            writer.Write(indentStr);
            writer.WriteLine("Prev delta offset: {0:X6}", prevDeltaOffset);
            if (Unknown5C != 0)
            {
                writer.Write(indentStr);
                writer.WriteLine("Unknown delta offset: {0:X8}", Unknown5C);
            }
            writer.Write(indentStr);
            writer.WriteLine("Project path: {0}", projectPath);
        }
    }

    public class ArchiveRevisionRecord : RevisionRecord
    {
        VssName name;
        string physical;
        string archivePath;

        public VssName Name { get { return name; } }
        public string Physical { get { return physical; } }
        public string ArchivePath { get { return archivePath; } }

        public override void Read(BufferReader reader, RecordHeader header)
        {
            base.Read(reader, header);

            name = reader.ReadName();
            physical = reader.ReadString(10);
            reader.Skip(2); // 0?
            archivePath = reader.ReadString(260);
            reader.Skip(4); // ?
        }

        public override void Dump(TextWriter writer, int indent)
        {
            base.Dump(writer, indent);
            string indentStr = DumpGetIndentString(indent);

            writer.Write(indentStr);
            writer.WriteLine("Name: {0} ({1})", name.ShortName, physical);
            writer.Write(indentStr);
            writer.WriteLine("Archive path: {0}", archivePath);
        }
    }
}
