
namespace SourceSafe.Jobs
{
    public class WorkerExceptionThrownEventArgs(Exception exception) : EventArgs
    {
        public Exception Exception { get; set; } = exception;
    };
}
