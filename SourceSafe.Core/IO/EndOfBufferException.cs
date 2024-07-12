
namespace SourceSafe.IO
{
    /// <summary>
    /// Exception thrown when the end of a buffer is reached unexpectedly.
    /// Intentionally different from EndOfStreamException to allow for more specific handling.
    /// </summary>
    public class EndOfBufferException : Exception
    {
        public EndOfBufferException()
        {
        }

        public EndOfBufferException(string message)
            : base(message)
        {
        }
    };
}
