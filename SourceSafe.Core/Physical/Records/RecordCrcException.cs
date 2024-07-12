
namespace SourceSafe.Physical.Records
{
    /// <summary>
    /// Exception thrown when the CRC stored in a record does not match the expected value.
    /// </summary>
    public sealed class RecordCrcException : RecordExceptionBase
    {
        public RecordHeader Header { get; init; }

        public RecordCrcException(RecordHeader header)
        {
            Header = header;
        }

        public RecordCrcException(RecordHeader header, string message)
            : base(message)
        {
            Header = header;
        }
    }
}
