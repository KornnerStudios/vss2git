
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

        public override void Dump(TextWriter writer, int indent)
        {
            base.Dump(writer, indent);
            string indentStr = IO.OutputUtil.GetIndentString(indent);

            writer.Write(indentStr);
            writer.WriteLine("Name: {0} ({1})", Name.ShortName, Physical);
            writer.Write(indentStr);
            writer.WriteLine("Archive path: {0}", ArchivePath);
        }
    };
}
