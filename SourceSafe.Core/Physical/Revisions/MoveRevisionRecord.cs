
namespace SourceSafe.Physical.Revisions
{
    public sealed class MoveRevisionRecord : RevisionRecordBase
    {
        public string ProjectPath { get; private set; } = "";
        public VssName Name { get; private set; }
        public string Physical { get; private set; } = "";

        protected override void ReadInternal(IO.VssBufferReader reader)
        {
            base.ReadInternal(reader);

            ProjectPath = reader.ReadFileNameString();
            Name = reader.ReadName();
            Physical = reader.ReadPhysicalNameString10();
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
