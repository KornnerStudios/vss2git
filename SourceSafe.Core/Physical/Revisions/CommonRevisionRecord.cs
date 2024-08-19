
namespace SourceSafe.Physical.Revisions
{
    public sealed class CommonRevisionRecord : RevisionRecordBase
    {
        public VssName Name { get; private set; }
        public string Physical { get; private set; } = "";

        protected override void ReadInternal(IO.VssBufferReader reader)
        {
            base.ReadInternal(reader);

            Name = reader.ReadName();
            Physical = reader.ReadPhysicalNameString10();
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
