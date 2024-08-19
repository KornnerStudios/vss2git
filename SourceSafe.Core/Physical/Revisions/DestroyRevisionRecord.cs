
namespace SourceSafe.Physical.Revisions
{
    public sealed class DestroyRevisionRecord : RevisionRecordBase
    {
        short unkShort;

        public VssName Name { get; private set; }
        public string Physical { get; private set; } = "";

        protected override void ReadInternal(IO.VssBufferReader reader)
        {
            base.ReadInternal(reader);

            Name = reader.ReadName();
            unkShort = reader.ReadInt16(); // 0 or 1
            Physical = reader.ReadPhysicalNameString10();
        }

        public override void Dump(Analysis.AnalysisTextDumper textDumper)
        {
            base.Dump(textDumper);

            textDumper.WriteLine($"Name: {Name.ShortName} ({Physical})");
            if (unkShort != 0)
            {
                textDumper.IncreaseIndent();
                textDumper.WriteLine($"Unknown: {unkShort}");
                textDumper.DecreaseIndent();
            }
        }
    };
}
