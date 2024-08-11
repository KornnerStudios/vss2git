
namespace SourceSafe.Physical.Revisions
{
    public sealed class EditRevisionRecord : RevisionRecordBase
    {
        public int PrevDeltaOffset { get; private set; }
        public int Unknown5C { get; private set; }
        public string ProjectPath { get; private set; } = "";

        public static bool ReadCheckForNonZeroUnknown5C { get; set; } = false;
        public override void Read(
            IO.VssBufferReader reader,
            Records.RecordHeader header)
        {
            base.Read(reader, header);

            PrevDeltaOffset = reader.ReadInt32();
            Unknown5C = reader.ReadInt32();
#if DEBUG
            if (ReadCheckForNonZeroUnknown5C && Unknown5C != 0)
            {
                "".ToString(); // place a breakpoint as needed
            }
#endif // DEBUG
            ProjectPath = reader.ReadString(260);
        }

        public override void Dump(TextWriter writer, int indent)
        {
            base.Dump(writer, indent);
            string indentStr = IO.OutputUtil.GetIndentString(indent);

            writer.Write(indentStr);
            writer.WriteLine("Prev delta offset: {0:X6}", PrevDeltaOffset);
            if (Unknown5C != 0)
            {
                writer.Write(indentStr);
                writer.WriteLine("Unknown delta offset: {0:X8}", Unknown5C);
            }
            writer.Write(indentStr);
            writer.WriteLine("Project path: {0}", ProjectPath);
        }
    };
}
