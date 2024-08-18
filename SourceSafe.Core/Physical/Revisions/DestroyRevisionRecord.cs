
namespace SourceSafe.Physical.Revisions
{
    public sealed class DestroyRevisionRecord : RevisionRecordBase
    {
        short unkShort;

        public VssName Name { get; private set; }
        public string Physical { get; private set; } = "";

        public override void Read(
            IO.VssBufferReader reader,
            Records.RecordHeader header)
        {
            base.Read(reader, header);

            Name = reader.ReadName();
            unkShort = reader.ReadInt16(); // 0 or 1
            Physical = reader.ReadString(10);
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
