
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

        public override void Dump(Analysis.AnalysisTextDumper textDumper)
        {
            textDumper.WriteLine($"Prev branch offset: {PrevBranchOffset:X6}");
            textDumper.WriteLine($"Branch file: {BranchFile}");
        }
    };
}
