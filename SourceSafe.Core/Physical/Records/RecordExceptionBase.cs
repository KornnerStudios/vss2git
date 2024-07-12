
namespace SourceSafe.Physical.Records
{
    /// <summary>
    /// Base class for exceptions thrown when an invalid record is read.
    /// </summary>
    public abstract class RecordExceptionBase : Exception
    {
        protected RecordExceptionBase()
        {
        }

        protected RecordExceptionBase(string message)
            : base(message)
        {
        }

        protected RecordExceptionBase(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    };
}
