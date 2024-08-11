
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

        public override void Dump(TextWriter writer, int indent)
        {
            base.Dump(writer, indent);
            string indentStr = IO.OutputUtil.GetIndentString(indent);

            writer.Write(indentStr);
            writer.WriteLine($"Project path: {ProjectPath}");
            writer.Write(indentStr);
            writer.WriteLine($"Name: {Name.ShortName} ({Physical})");
        }
    };
}
