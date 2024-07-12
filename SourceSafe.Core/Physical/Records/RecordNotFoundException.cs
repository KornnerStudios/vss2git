
namespace SourceSafe.Physical.Records
{
    /// <summary>
    /// Exception thrown when a particular record cannot be found.
    /// </summary>
    public sealed class RecordNotFoundException : RecordExceptionBase
    {
        public RecordNotFoundException()
        {
        }

        public RecordNotFoundException(string message)
            : base(message)
        {
        }
    };
}
