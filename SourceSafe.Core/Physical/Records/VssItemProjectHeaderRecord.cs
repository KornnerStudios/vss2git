
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

        public override void Read(IO.VssBufferReader reader, RecordHeader header)
        {
            base.Read(reader, header);

            ParentProject = reader.ReadString(260);
            ParentFile = reader.ReadString(8);
            reader.SkipAssumedToBeAllZeros(4); // reserved; always 0
            TotalItems = reader.ReadInt16();
            Subprojects = reader.ReadInt16();
        }

        public override void Dump(TextWriter writer, int indent)
        {
            base.Dump(writer, indent);
            string indentStr = IO.OutputUtil.GetIndentString(indent);

            writer.Write(indentStr);
            writer.WriteLine("Parent project: {0}", ParentProject);
            writer.Write(indentStr);
            writer.WriteLine("Parent file: {0}", ParentFile);
            writer.Write(indentStr);
            writer.WriteLine("Total items: {0}", TotalItems);
            writer.Write(indentStr);
            writer.WriteLine("Subprojects: {0}", Subprojects);
        }
    };
}
