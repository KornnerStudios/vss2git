
namespace SourceSafe.Physical.Records
{
    /// <summary>
    /// Base class for item VSS header records.
    /// </summary>
    public abstract class VssItemHeaderRecordBase : VssRecordBase
    {
        public const string SIGNATURE = "DH";
        public override string Signature => SIGNATURE;

        public VssItemType ItemType { get; private set; }
        /// <summary>
        /// Stores the number of log entry ("EL") chunks stored in the file.
        /// </summary>
        public int Revisions { get; private set; }
        public VssName Name { get; private set; }
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
        public string DataExt { get; private set; } = "";
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

        public bool IsProject => ItemType == VssItemType.Project;
        public bool IsFile => ItemType == VssItemType.File;

        protected VssItemHeaderRecordBase(VssItemType itemType)
        {
            ItemType = itemType;
        }

        public override void Read(IO.VssBufferReader reader, RecordHeader header)
        {
            base.Read(reader, header);

            ItemType = (VssItemType)reader.ReadInt16();
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
            string indentStr = IO.OutputUtil.GetIndentString(indent);

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
    };
}
