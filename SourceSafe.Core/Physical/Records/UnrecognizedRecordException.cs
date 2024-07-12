
namespace SourceSafe.Physical.Records
{
    /// <summary>
    /// Exception thrown when an unrecognized record type is encountered.
    /// </summary>
    public sealed class UnrecognizedRecordException : RecordExceptionBase
    {
        public RecordHeader Header { get; init; }

        public UnrecognizedRecordException(RecordHeader header)
        {
            Header = header;
        }

        public UnrecognizedRecordException(RecordHeader header, string message)
            : base(message)
        {
            Header = header;
        }
    };
}
