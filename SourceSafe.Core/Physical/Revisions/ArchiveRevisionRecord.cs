
namespace SourceSafe.Physical.Revisions
{
    public sealed class ArchiveRevisionRecord : RevisionRecordBase
    {
        public VssName Name { get; private set; }
        public string Physical { get; private set; } = "";
        public string ArchivePath { get; private set; } = "";

        public override void Read(
            IO.VssBufferReader reader,
            Records.RecordHeader header)
        {
            base.Read(reader, header);

            Name = reader.ReadName();
            Physical = reader.ReadString(10);
            reader.SkipUnknown(2); // 0?
            ArchivePath = reader.ReadString(260);
            reader.SkipUnknown(4); // ?
        }

        public override void Dump(Analysis.AnalysisTextDumper textDumper)
        {
            base.Dump(textDumper);

            textDumper.WriteLine($"Name: {Name.ShortName} ({Physical})");
            textDumper.WriteLine($"Archive path: {ArchivePath}");
        }
    };
}
