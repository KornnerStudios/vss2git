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
    /// Flags enumeration for items in project.
    /// </summary>
    /// <author>Trevor Robinson</author>
    [Flags]
    public enum ProjectEntryFlags
    {
        None,
        Deleted = 0x01,
        Binary = 0x02,
        LatestOnly = 0x04,
        Shared = 0x08,
    }

    /// <summary>
    /// VSS record for representing an item stored in particular project.
    /// </summary>
    /// <author>Trevor Robinson</author>
    /// <seealso cref="VssScanChild"/>
    public sealed class ProjectEntryRecord : VssRecord
    {
        public const string SIGNATURE = "JP";
        public override string Signature => SIGNATURE;

        public ItemType ItemType { get; private set; }
        public ProjectEntryFlags Flags { get; private set; }
        public VssName Name { get; private set; }
        public short PinnedVersion { get; private set; }
        /// <summary>
        /// Name of the database file name, in "aaaaaaaa" format.
        /// </summary>
        public string Physical { get; private set; }

        public override void Read(BufferReader reader, RecordHeader header)
        {
            base.Read(reader, header);

            ItemType = (ItemType)reader.ReadInt16();
            Flags = (ProjectEntryFlags)reader.ReadInt16();
            Name = reader.ReadName();
            PinnedVersion = reader.ReadInt16();
            Physical = reader.ReadString(10);
        }

        public override void Dump(TextWriter writer, int indent)
        {
            string indentStr = DumpGetIndentString(indent);

            writer.Write(indentStr);
            writer.WriteLine($"Item Type: {ItemType} - Name: {Name.ShortName} ({Physical})");
            writer.Write(indentStr);
            writer.WriteLine($"Flags: {Flags}");
            writer.Write(indentStr);
            writer.WriteLine($"Pinned version: {PinnedVersion}");
        }
    }
}
