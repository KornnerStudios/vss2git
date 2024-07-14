
namespace SourceSafe.Physical.Records
{
    /// <summary>
    /// VSS record representing a comment message.
    /// </summary>
    public sealed class CommentRecord : VssRecordBase
    {
        public const string SIGNATURE = "MC";

        public override string Signature => SIGNATURE;
        public string Comment { get; private set; } = "";

        public override void Read(IO.VssBufferReader reader, RecordHeader header)
        {
            base.Read(reader, header);

            Comment = reader.ReadString(reader.RemainingSize);
        }

        public override void Dump(TextWriter writer, int indent)
        {
            string indentStr = IO.OutputUtil.GetIndentString(indent);

            writer.Write(indentStr);
            writer.WriteLine("{0}", Comment);
        }
    };
}
