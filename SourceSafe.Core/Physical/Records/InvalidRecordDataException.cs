
namespace SourceSafe.Physical.Records
{
    public class InvalidRecordDataException : RecordExceptionBase
    {
        public InvalidRecordDataException(string message)
            : base(message)
        {
        }
    };
}
