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

using System.IO;

namespace Hpdi.VssPhysicalLib
{
    /// <summary>
    /// Enumeration indicating whether an item is a project or a file.
    /// </summary>
    /// <author>Trevor Robinson</author>
    public enum ItemType
    {
        Project = 1,
        File = 2,
    }

    /// <summary>
    /// Base class for item VSS header records.
    /// </summary>
    /// <author>Trevor Robinson</author>
    public abstract class ItemHeaderRecord : VssRecord
    {
        public const string SIGNATURE = "DH";

        public override string Signature => SIGNATURE;
        public ItemType ItemType { get; private set; }
        public int Revisions { get; private set; }
        public VssName Name { get; private set; }
        public int FirstRevision { get; private set; }
        public string DataExt { get; private set; }
        public int FirstRevOffset { get; private set; }
        /// <summary>
        /// File offset of the last RecordHeader and its payload
        /// </summary>
        public int LastRevOffset { get; private set; }
        /// <summary>
        /// The assumed EOF, which may not actually match the literal EOF,
        /// and which may include now-truncated bytes starting from the
        /// true end of LastRevOffset's payload and EofOffset.
        /// </summary>
        public int EofOffset { get; private set; }
        public int RightsOffset { get; private set; }

        protected ItemHeaderRecord(ItemType itemType)
        {
            ItemType = itemType;
        }

        public override void Read(BufferReader reader, RecordHeader header)
        {
            base.Read(reader, header);

            ItemType = (ItemType)reader.ReadInt16();
            Revisions = reader.ReadInt16();
            Name = reader.ReadName();
            FirstRevision = reader.ReadInt16();
            DataExt = reader.ReadString(2);
            FirstRevOffset = reader.ReadInt32();
            LastRevOffset = reader.ReadInt32();
            EofOffset = reader.ReadInt32();
            RightsOffset = reader.ReadInt32();
            reader.Skip(16); // reserved; always 0
        }

        public override void Dump(TextWriter writer, int indent)
        {
            string indentStr = DumpGetIndentString(indent);

            writer.Write(indentStr);
            writer.WriteLine("Item Type: {0} - Revisions: {1} - Name: {2}",
                ItemType, Revisions, Name.ShortName);
            writer.Write(indentStr);
            writer.WriteLine("Name offset: {0:X6}", Name.NameFileOffset);
            writer.Write(indentStr);
            writer.WriteLine("First revision: #{0:D3}", FirstRevision);
            writer.Write(indentStr);
            writer.WriteLine("Data extension: {0}", DataExt);
            writer.Write(indentStr);
            writer.WriteLine("First/last rev offset: {0:X6}/{1:X6}",
                FirstRevOffset, LastRevOffset);
            writer.Write(indentStr);
            writer.WriteLine("EOF offset: {0:X6}", EofOffset);
            writer.Write(indentStr);
            writer.WriteLine("Rights offset: {0:X8}", RightsOffset);
        }
    }
}
