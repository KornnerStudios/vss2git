
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
        private int mUnknown0C;

        protected override void ReadInternal(IO.VssBufferReader reader)
        {
            PrevBranchOffset = reader.ReadInt32();
            BranchFile = reader.ReadPhysicalNameString8();
            // #REVIEW are these meaningful, or garbage bytes?
            mUnknown0C = reader.ReadInt32();
        }

        public override void Dump(Analysis.AnalysisTextDumper textDumper)
        {
            textDumper.WriteLine($"Prev branch offset: {PrevBranchOffset:X6}");
            textDumper.WriteLine($"Branch file: {BranchFile}");
            if (mUnknown0C != 0)
            {
                textDumper.WriteLine($"{SIGNATURE} Unknown 0C: {mUnknown0C:X8}");
            }
        }
    };
}
