
namespace SourceSafe.Physical.Revisions
{
    public sealed class RenameRevisionRecord : RevisionRecordBase
    {
        public VssName Name { get; private set; }
        public VssName OldName { get; private set; }
        public string Physical { get; private set; } = "";

        protected override void ReadInternal(IO.VssBufferReader reader)
        {
            base.ReadInternal(reader);

            Name = reader.ReadName();
            OldName = reader.ReadName();
            Physical = reader.ReadPhysicalNameString10();
        }

        public override void Dump(Analysis.AnalysisTextDumper textDumper)
        {
            base.Dump(textDumper);

            textDumper.WriteLine($"Name: {OldName.ShortName} -> {Name.ShortName} ({Physical})");
        }
    };
}
