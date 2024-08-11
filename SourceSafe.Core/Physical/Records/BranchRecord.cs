
namespace SourceSafe.Physical.Records
{
    /// <summary>
    /// VSS record representing a branch file.
    /// </summary>
    public sealed class BranchRecord : VssRecordBase
    {
        public const string SIGNATURE = "BF";

        public override string Signature => SIGNATURE;
        public int PrevBranchOffset { get; private set; }
        public string BranchFile { get; private set; } = "";

        public override void Read(
            IO.VssBufferReader reader,
            RecordHeader header)
        {
            base.Read(reader, header);

            PrevBranchOffset = reader.ReadInt32();
            BranchFile = reader.ReadString(12);
        }

        public override void Dump(TextWriter writer, int indent)
        {
            string indentStr = IO.OutputUtil.GetIndentString(indent);

            writer.Write(indentStr);
            writer.WriteLine("Prev branch offset: {0:X6}", PrevBranchOffset);
            writer.Write(indentStr);
            writer.WriteLine("Branch file: {0}", BranchFile);
        }
    };
}
