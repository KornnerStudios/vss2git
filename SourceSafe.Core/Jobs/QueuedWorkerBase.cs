using System.Windows.Forms;

namespace SourceSafe.Jobs
{
    /// <summary>
    /// Base class for queued workers in the application.
    /// </summary>
    public abstract class QueuedWorkerBase
    {
        protected readonly TrackedWorkQueue mWorkQueue;
        protected readonly IO.SimpleLogger mLogger;

        public QueuedWorkerBase(TrackedWorkQueue workQueue, IO.SimpleLogger logger)
        {
            mWorkQueue = workQueue;
            mLogger = logger;
        }

        protected void LogStatus(object work, string status)
        {
            mWorkQueue.SetStatus(work, status);
            mLogger.WriteLine(status);
        }

        protected string LogException(Exception exception)
        {
            string message = SourceSafe.Exceptions.ExceptionFormatter.Format(exception);
            LogException(exception, message);
            return message;
        }

        protected void LogException(Exception exception, string message)
        {
            mLogger.WriteLine($"ERROR: {message}");
            mLogger.WriteLine(exception);
        }

        protected void ReportError(string message)
        {
            DialogResult button = MessageBox.Show(message, "Error", MessageBoxButtons.OKCancel, MessageBoxIcon.Error);
            if (button == DialogResult.Cancel)
            {
                mWorkQueue.Abort();
            }
        }
    };
}
