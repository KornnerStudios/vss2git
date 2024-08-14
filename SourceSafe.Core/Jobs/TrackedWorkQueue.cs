using System.Diagnostics;

namespace SourceSafe.Jobs
{
    /// <summary>
    /// Extends the simple work queue with support for tracking worker status and exceptions.
    /// </summary>
    public sealed class TrackedWorkQueue : SimpleWorkQueue
    {
        private readonly ManualResetEvent mIdleEvent = new(true);
        private readonly Stopwatch mStopwatch = new();
        private readonly List<Exception> mWorkExceptions = [];
        private readonly Dictionary<WorkerCallback, string?> mWorkStatuses = [];
        private WorkerCallback? mLastStatusWork;

        public string? LastStatus { get; private set; }

        public TrackedWorkQueue()
        {
        }

        public TrackedWorkQueue(int maxThreads)
            : base(maxThreads)
        {
        }

        public TimeSpan ActiveTime => mStopwatch.Elapsed;

        public WaitHandle IdleEvent => mIdleEvent;

        public event EventHandler? Idle;
        public event EventHandler<WorkerExceptionThrownEventArgs>? ExceptionThrown;

        public void WaitIdle()
        {
            mIdleEvent.WaitOne();
        }

        public ICollection<Exception>? FetchExceptions()
        {
            lock (mWorkExceptions)
            {
                if (mWorkExceptions.Count > 0)
                {
                    var result = new List<Exception>(mWorkExceptions);
                    mWorkExceptions.Clear();
                    return result;
                }
            }
            return null;
        }

        public string? GetStatus(WorkerCallback work)
        {
            string? result;
            lock (mWorkStatuses)
            {
                mWorkStatuses.TryGetValue(work, out result);
            }
            return result;
        }

        public void SetStatus(WorkerCallback work, string? status)
        {
            lock (mWorkStatuses)
            {
                // only allow status to be set if key is already present,
                // so we know that it will be removed in OnStop
                if (mWorkStatuses.ContainsKey(work))
                {
                    mWorkStatuses[work] = status;
                    if (string.IsNullOrEmpty(status))
                    {
                        WorkStatusCleared(work);
                    }
                    else
                    {
                        mLastStatusWork = work;
                        LastStatus = status;
                    }
                }
            }
        }

        public void ClearStatus(WorkerCallback work)
        {
            SetStatus(work, null);
        }

        protected override void OnActive()
        {
            base.OnActive();
            mIdleEvent.Reset();
            mStopwatch.Start();
        }

        protected override void OnIdle()
        {
            base.OnIdle();
            mStopwatch.Stop();
            mIdleEvent.Set();

            Idle?.Invoke(this, EventArgs.Empty);
        }

        protected override void OnStart(WorkerCallback work)
        {
            base.OnStart(work);
            lock (mWorkStatuses)
            {
                mWorkStatuses[work] = null;
            }
        }

        protected override void OnStop(WorkerCallback work)
        {
            base.OnStop(work);
            lock (mWorkStatuses)
            {
                mWorkStatuses.Remove(work);
                WorkStatusCleared(work);
            }
        }

        protected override void OnException(WorkerCallback work, Exception e)
        {
            base.OnException(work, e);

            EventHandler<WorkerExceptionThrownEventArgs>? handler = ExceptionThrown;
            if (handler != null)
            {
                WorkerExceptionThrownEventArgs eventArgs = new(e);
                handler(this, eventArgs);
            }

            lock (mWorkExceptions)
            {
                mWorkExceptions.Add(e);
            }
        }

        // Assumes work status lock is held.
        private void WorkStatusCleared(WorkerCallback work)
        {
            if (work == mLastStatusWork)
            {
                mLastStatusWork = null;
                LastStatus = null;

                foreach (KeyValuePair<WorkerCallback, string?> entry in mWorkStatuses)
                {
                    if (!string.IsNullOrEmpty(entry.Value))
                    {
                        mLastStatusWork = entry.Key;
                        LastStatus = entry.Value;
                        break;
                    }
                }
            }
        }
    };
}
