
namespace SourceSafe.Physical.Revisions
{
    public sealed class RenameRevisionRecord : RevisionRecordBase
    {
        public VssName Name { get; private set; }
        public VssName OldName { get; private set; }
        public string Physical { get; private set; } = "";

        public override void Read(
            IO.VssBufferReader reader,
            Records.RecordHeader header)
        {
            base.Read(reader, header);

            Name = reader.ReadName();
            OldName = reader.ReadName();
            Physical = reader.ReadString(10);
        }

        public override void Dump(Analysis.AnalysisTextDumper textDumper)
        {
            base.Dump(textDumper);

            textDumper.WriteLine($"Name: {OldName.ShortName} -> {Name.ShortName} ({Physical})");
        }
    };
}
