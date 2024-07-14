
namespace SourceSafe.Physical.Records
{
    /// <summary>
    /// VSS header record for a file.
    /// </summary>
    public sealed class VssItemFileHeaderRecord : VssItemHeaderRecordBase
    {
        public VssItemFileFlags Flags { get; private set; }
        public string BranchFile { get; private set; } = "";
        public int BranchOffset { get; private set; }
        public int ProjectOffset { get; private set; }
        /// <summary>
        /// Number of branch chunks that are stored in the file's change log.
        /// </summary>
        public int BranchCount { get; private set; }
        /// <summary>
        /// This is the number of valid parent chunks. A new file starts off
        /// with one parent chunk. Each time the file is shared, a new parent
        /// chunk is appended to the file. Parent chunks are never deleted
        /// from the file. However, if a file is branched, the associated
        /// parent chunk has the parent name zeroed out.
        ///
        /// This field stores the number of currently valid parent chunks
        /// that are in the file. Chunks related to branched file are not
        /// part of this count.
        ///
        /// Parent chunks have the offset of the previous parent. Could try
        /// to traverse backwards from the last parent, counting the number
        /// that are actually in the list. Maybe that is what it adds up to.
        /// </summary>
        public int ProjectCount { get; private set; }
        public int FirstCheckoutOffset { get; private set; }
        public int LastCheckoutOffset { get; private set; }
        public uint DataCrc { get; private set; }
        public DateTime LastRevDateTime { get; private set; }
        public DateTime ModificationDateTime { get; private set; }
        public DateTime CreationDateTime { get; private set; }

        public VssItemFileHeaderRecord()
            : base(VssItemType.File)
        {
        }

        public override void Read(IO.VssBufferReader reader, RecordHeader header)
        {
            base.Read(reader, header);

            Flags = (VssItemFileFlags)reader.ReadInt16();
            BranchFile = reader.ReadString(8);
            reader.SkipAssumedToBeAllZeros(2); // reserved; always 0
            // VssTree::AssembleDirectoryLinks: lastBranchOffset
            BranchOffset = reader.ReadInt32();
            // VssTree::AssembleDirectoryLinks: lastParentOffset
            ProjectOffset = reader.ReadInt32();
            BranchCount = reader.ReadInt16();
            // VssTree::AssembleDirectoryLinks: parentCount
            ProjectCount = reader.ReadInt16();
            // VssTree::AssembleDirectoryLinks: checkoutActive
            FirstCheckoutOffset = reader.ReadInt32();
            // VssTree::AssembleDirectoryLinks: checkoutInactive
            LastCheckoutOffset = reader.ReadInt32();
            // VssTree::AssembleDirectoryLinks:
            // This is a 32-bit CRC of the current data file.  Note that this uses
            // CRC logic that starts XORing from 0 instead of -1.
            DataCrc = (uint)reader.ReadInt32();
            reader.SkipAssumedToBeAllZeros(8); // reserved; always 0
            // VssTree::AssembleDirectoryLinks: lastCheckinTime
            LastRevDateTime = reader.ReadDateTime();
            ModificationDateTime = reader.ReadDateTime();
            CreationDateTime = reader.ReadDateTime();
            // remaining appears to be trash

            // This is random, uninitialized junk. Frequently composed from
            // pieces of source code that was being checked in.
            reader.SkipKnownJunk(16);
            // Long string of data that is initialized to all zeroes.
            reader.SkipAssumedToBeAllZeros(200);
            // VssTree::AssembleDirectoryLinks reads these and prints an error
            // if projectCount > itemCount, but does nothing else with them.
            reader.ReadInt16(); // itemCount
            reader.ReadInt16(); // projectCount
        }

        public override void Dump(TextWriter writer, int indent)
        {
            base.Dump(writer, indent);
            string indentStr = IO.OutputUtil.GetIndentString(indent);

            writer.Write(indentStr);
            writer.WriteLine($"Flags: {Flags}");
            writer.Write(indentStr);
            writer.WriteLine($"Branched from file: {BranchFile}");
            writer.Write(indentStr);
            writer.WriteLine($"Branch offset: {BranchOffset:X6}");
            writer.Write(indentStr);
            writer.WriteLine($"Branch count: {BranchCount}");
            writer.Write(indentStr);
            writer.WriteLine($"Project offset: {ProjectOffset:X6}");
            writer.Write(indentStr);
            writer.WriteLine($"Project count: {ProjectCount}");
            writer.Write(indentStr);
            writer.WriteLine($"First/last checkout offset: {FirstCheckoutOffset:X6}/{LastCheckoutOffset:X6}");
            writer.Write(indentStr);
            writer.WriteLine($"Data CRC: {DataCrc:X8}");
            writer.Write(indentStr);
            writer.WriteLine($"Last revision time: {LastRevDateTime}");
            writer.Write(indentStr);
            writer.WriteLine($"Modification time: {ModificationDateTime}");
            writer.Write(indentStr);
            writer.WriteLine($"Creation time: {CreationDateTime}");
        }
    };
}
