
namespace SourceSafe.Physical.Revisions
{
    public sealed class CommonRevisionRecord : RevisionRecordBase
    {
        public VssName Name { get; private set; }
        public string Physical { get; private set; } = "";

        public override void Read(IO.VssBufferReader reader, Records.RecordHeader header)
        {
            base.Read(reader, header);

            Name = reader.ReadName();
            Physical = reader.ReadString(10);
        }

        public override void Dump(Analysis.AnalysisTextDumper textDumper)
        {
            base.Dump(textDumper);

            if (string.IsNullOrEmpty(Name.ShortName) && string.IsNullOrEmpty(Physical))
            {
                // Should be expected for CreateProject
                textDumper.WriteLine($"Name: NONE!");
            }
            else
            {
                textDumper.WriteLine($"Name: {Name.ShortName} ({Physical})");
            }
        }
    };
}
