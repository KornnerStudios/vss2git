
namespace SourceSafe.Physical.Revisions
{
    public sealed class BranchRevisionRecord : RevisionRecordBase
    {
        public VssName Name { get; private set; }
        public string Physical { get; private set; } = "";
        public string BranchFile { get; private set; } = "";

        protected override void ReadInternal(IO.VssBufferReader reader)
        {
            base.ReadInternal(reader);

            Name = reader.ReadName();
            Physical = reader.ReadPhysicalNameString10();
            BranchFile = reader.ReadPhysicalNameString10();
        }

        public override void Dump(Analysis.AnalysisTextDumper textDumper)
        {
            base.Dump(textDumper);

            textDumper.WriteLine($"Name: {Name.ShortName} ({Physical})");
            textDumper.WriteLine($"Branched from file: {BranchFile}");
        }
    };
}
