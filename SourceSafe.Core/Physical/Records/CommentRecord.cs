
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

        protected override void ReadInternal(IO.VssBufferReader reader)
        {
            Comment = reader.ReadString(reader.RemainingSize);
        }

        public override void Dump(Analysis.AnalysisTextDumper textDumper)
        {
            if (textDumper.VerboseFilter(!string.IsNullOrEmpty(Comment)))
            {
                textDumper.WriteLine(Comment);
            }
        }
    };
}
