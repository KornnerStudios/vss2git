
namespace SourceSafe.Physical.Records
{
    /// <summary>
    /// VSS record representing a project file.
    /// </summary>
    public sealed class ProjectRecord : VssRecordBase
    {
        public const string SIGNATURE = "PF";

        public override string Signature => SIGNATURE;
        public int PrevProjectOffset { get; private set; }
        public string ProjectFile { get; private set; } = "";
        private short mUnknown0C;
        private short mUnknown0E;

        protected override void ReadInternal(IO.VssBufferReader reader)
        {
            PrevProjectOffset = reader.ReadInt32();
            ProjectFile = reader.ReadPhysicalNameString8();
            // #REVIEW are these two shorts? or one short and padding?
            mUnknown0C = reader.ReadInt16();
            mUnknown0E = reader.ReadInt16();
        }

        public override void Dump(Analysis.AnalysisTextDumper textDumper)
        {
            textDumper.WriteLine($"Project file: {ProjectFile}");
            textDumper.WriteLine($"Prev project offset: {PrevProjectOffset:X6}");
            if (mUnknown0C != 0)
            {
                textDumper.WriteLine($"{SIGNATURE} Unknown 0C: {mUnknown0C:X4}");
            }
            if (mUnknown0E != 0)
            {
                textDumper.WriteLine($"{SIGNATURE} Unknown 0E: {mUnknown0E:X4}");
            }
        }
    };
}
