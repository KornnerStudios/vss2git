
namespace SourceSafe.Physical.Records
{
    /// <summary>
    /// Exception thrown when a truncated record is found.
    /// </summary>
    public sealed class RecordTruncatedException : RecordExceptionBase
    {
        public RecordTruncatedException()
        {
        }

        public RecordTruncatedException(string message)
            : base(message)
        {
        }
    };
}
