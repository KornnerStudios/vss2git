
namespace SourceSafe.Physical.Revisions
{
    public sealed class EditRevisionRecord : RevisionRecordBase
    {
        public int PrevDeltaOffset { get; private set; }
        public int Unknown5C { get; private set; }
        public string ProjectPath { get; private set; } = "";

        // #TODO add JSON flag to control this behavior
        public static bool ReadCheckForNonZeroUnknown5C { get; set; } = false;
        protected override void ReadInternal(IO.VssBufferReader reader)
        {
            base.ReadInternal(reader);

            PrevDeltaOffset = reader.ReadInt32();
            Unknown5C = reader.ReadInt32();
#if DEBUG
            if (ReadCheckForNonZeroUnknown5C && Unknown5C != 0)
            {
                "".ToString(); // place a breakpoint as needed
            }
#endif // DEBUG
            ProjectPath = reader.ReadFileNameString();
        }

        public override void Dump(Analysis.AnalysisTextDumper textDumper)
        {
            base.Dump(textDumper);

            if (textDumper.VerboseFilter(PrevDeltaOffset != 0))
            {
                textDumper.WriteLine($"Prev delta offset: {PrevDeltaOffset:X6}");
            }
            if (Unknown5C != 0)
            {
                textDumper.WriteLine($"Unknown delta offset: {Unknown5C:X8}");
            }
            if (!string.IsNullOrEmpty(ProjectPath))
            {
                textDumper.WriteLine($"Project path: {ProjectPath}");
            }
        }
    };
}
