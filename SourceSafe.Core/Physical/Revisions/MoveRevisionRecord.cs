
namespace SourceSafe.Physical.Revisions
{
    public sealed class MoveRevisionRecord : RevisionRecordBase
    {
        public string ProjectPath { get; private set; } = "";
        public VssName Name { get; private set; }
        public string Physical { get; private set; } = "";

        public override void Read(
            IO.VssBufferReader reader,
            Records.RecordHeader header)
        {
            base.Read(reader, header);

            ProjectPath = reader.ReadString(260);
            Name = reader.ReadName();
            Physical = reader.ReadString(10);
        }

        public override void Dump(Analysis.AnalysisTextDumper textDumper)
        {
            base.Dump(textDumper);

            if (textDumper.VerboseFilter(!string.IsNullOrEmpty(ProjectPath)))
            {
                textDumper.WriteLine($"Project path: {ProjectPath}");
            }
            textDumper.WriteLine($"Name: {Name.ShortName} ({Physical})");
        }
    };
}
