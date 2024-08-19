
namespace SourceSafe.Physical.Revisions
{
    public sealed class ArchiveRevisionRecord : RevisionRecordBase
    {
        public VssName Name { get; private set; }
        public string Physical { get; private set; } = "";
        public string ArchivePath { get; private set; } = "";

        protected override void ReadInternal(IO.VssBufferReader reader)
        {
            base.ReadInternal(reader);

            Name = reader.ReadName();
            Physical = reader.ReadPhysicalNameString10();
            reader.SkipUnknown(2); // 0?
            ArchivePath = reader.ReadFileNameString();
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
