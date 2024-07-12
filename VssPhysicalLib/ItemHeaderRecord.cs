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
using SourceSafe.Physical.Records;

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
        /// <summary>
        /// Stores the number of log entry ("EL") chunks stored in the file.
        /// </summary>
        public int Revisions { get; private set; }
        public SourceSafe.Physical.VssName Name { get; private set; }
        /// <summary>
        /// If the file has been branched, this field will contain the version
        /// number at which it was branched.
        ///
        /// If the branch number is 1, the file has never been branched.
        /// </summary>
        public int FirstRevision { get; private set; }
        /// <remarks>
        /// VssScanHeader notes:
        ///     This is the file extension of the associated data file, which will
        ///     always be ".A" or ".B". This is ignored here, since this code will
        ///     grab whichever file it finds, regardless of extension. VSS will
        ///     alternate extensions whenever it rewrites files, and this field
        ///     indicates which it used last. It would be safer to pay attention
        ///     to this field, since some online sources indicate that VSS sometimes
        ///     glitches and leaves both files behind after a merge. This was never
        ///     observed to be the case with the test DB, so that test was never
        ///     needed with this code.
        /// </remarks>
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

        public override void Read(SourceSafe.IO.VssBufferReader reader, RecordHeader header)
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
            reader.SkipAssumedToBeAllZeros(16);
        }

        public override void Dump(TextWriter writer, int indent)
        {
            string indentStr = DumpGetIndentString(indent);

            writer.Write(indentStr);
            writer.WriteLine($"Item Type: {ItemType} - Revisions: {Revisions} - Name: {Name.ShortName}");
            writer.Write(indentStr);
            writer.WriteLine($"Name offset: {Name.NameFileOffset:X6}");
            writer.Write(indentStr);
            writer.WriteLine($"First revision: #{FirstRevision:D3}");
            writer.Write(indentStr);
            writer.WriteLine($"Data extension: {DataExt}");
            writer.Write(indentStr);
            writer.WriteLine($"First/last rev offset: {FirstRevOffset:X6}/{LastRevOffset:X6}");
            writer.Write(indentStr);
            writer.WriteLine($"EOF offset: {EofOffset:X6}");
            writer.Write(indentStr);
            writer.WriteLine($"Rights offset: {RightsOffset:X8}");
        }
    }
}
