
namespace SourceSafe.Physical.Records
{
    /// <summary>
    /// VSS header record for a project.
    /// </summary>
    public sealed class VssItemProjectHeaderRecord : VssItemHeaderRecordBase
    {
        public string ParentProject { get; private set; } = "";
        public string ParentFile { get; private set; } = "";
        public int TotalItems { get; private set; }
        public int Subprojects { get; private set; }

        public VssItemProjectHeaderRecord()
            : base(VssItemType.Project)
        {
        }

        protected override void ReadInternal(IO.VssBufferReader reader)
        {
            base.ReadInternal(reader);

            ParentProject = reader.ReadFileNameString();
            ParentFile = reader.ReadPhysicalNameString8();
            reader.SkipAssumedToBeAllZeros(4); // reserved; always 0
            TotalItems = reader.ReadInt16();
            Subprojects = reader.ReadInt16();
        }

        public override void Dump(Analysis.AnalysisTextDumper textDumper)
        {
            base.Dump(textDumper);

            textDumper.WriteLine($"Parent project: {ParentProject}");
            textDumper.WriteLine($"Parent file: {ParentFile}");
            textDumper.WriteLine($"Total items: {TotalItems}");
            textDumper.WriteLine($"Subprojects: {Subprojects}");
        }
    };
}
