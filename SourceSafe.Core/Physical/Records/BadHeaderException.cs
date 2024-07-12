
namespace SourceSafe.Physical.Records
{
    /// <summary>
    /// Exception thrown when an invalid record header is read.
    /// </summary>
    public sealed class BadHeaderException : RecordExceptionBase
    {
        public BadHeaderException()
        {
        }

        public BadHeaderException(string message)
            : base(message)
        {
        }

        public BadHeaderException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    };
}
