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
    /// Flags enumeration for a VSS file.
    /// </summary>
    /// <author>Trevor Robinson</author>
    [Flags]
    public enum FileFlags
    {
        None,
        Locked = 0x01,
        Binary = 0x02,
        LatestOnly = 0x04,
        // #TODO 0x08?
        // #TODO 0x10?
        // #TODO 0x14?
        // #TODO 0x18?
        // #TODO 0x1C?
        Shared = 0x20,
        CheckedOut = 0x40,
    }

    /// <summary>
    /// VSS header record for a file.
    /// </summary>
    /// <author>Trevor Robinson</author>
    public sealed class FileHeaderRecord : ItemHeaderRecord
    {
        public FileFlags Flags { get; private set; }
        public string BranchFile { get; private set; }
        public int BranchOffset { get; private set; }
        public int ProjectOffset { get; private set; }
        public int BranchCount { get; private set; }
        public int ProjectCount { get; private set; }
        public int FirstCheckoutOffset { get; private set; }
        public int LastCheckoutOffset { get; private set; }
        public uint DataCrc { get; private set; }
        public DateTime LastRevDateTime { get; private set; }
        public DateTime ModificationDateTime { get; private set; }
        public DateTime CreationDateTime { get; private set; }

        public FileHeaderRecord()
            : base(ItemType.File)
        {
        }

        public override void Read(BufferReader reader, RecordHeader header)
        {
            base.Read(reader, header);

            Flags = (FileFlags)reader.ReadInt16();
            BranchFile = reader.ReadString(8);
            reader.Skip(2); // reserved; always 0
            BranchOffset = reader.ReadInt32();
            ProjectOffset = reader.ReadInt32();
            BranchCount = reader.ReadInt16();
            ProjectCount = reader.ReadInt16();
            FirstCheckoutOffset = reader.ReadInt32();
            LastCheckoutOffset = reader.ReadInt32();
            DataCrc = (uint)reader.ReadInt32();
            reader.Skip(8); // reserved; always 0
            LastRevDateTime = reader.ReadDateTime();
            ModificationDateTime = reader.ReadDateTime();
            CreationDateTime = reader.ReadDateTime();
            // remaining appears to be trash
        }

        public override void Dump(TextWriter writer, int indent)
        {
            base.Dump(writer, indent);
            string indentStr = DumpGetIndentString(indent);

            writer.Write(indentStr);
            writer.WriteLine("Flags: {0}", Flags);
            writer.Write(indentStr);
            writer.WriteLine("Branched from file: {0}", BranchFile);
            writer.Write(indentStr);
            writer.WriteLine("Branch offset: {0:X6}", BranchOffset);
            writer.Write(indentStr);
            writer.WriteLine("Branch count: {0}", BranchCount);
            writer.Write(indentStr);
            writer.WriteLine("Project offset: {0:X6}", ProjectOffset);
            writer.Write(indentStr);
            writer.WriteLine("Project count: {0}", ProjectCount);
            writer.Write(indentStr);
            writer.WriteLine("First/last checkout offset: {0:X6}/{1:X6}",
                FirstCheckoutOffset, LastCheckoutOffset);
            writer.Write(indentStr);
            writer.WriteLine("Data CRC: {0:X8}", DataCrc);
            writer.Write(indentStr);
            writer.WriteLine("Last revision time: {0}", LastRevDateTime);
            writer.Write(indentStr);
            writer.WriteLine("Modification time: {0}", ModificationDateTime);
            writer.Write(indentStr);
            writer.WriteLine("Creation time: {0}", CreationDateTime);
        }
    }
}
