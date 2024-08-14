
namespace SourceSafe.Jobs
{
    /// <summary>
    /// Simple work queue over a bounded number of thread-pool threads.
    /// </summary>
    // This is abstract only because nothing instances it, but it could be a concrete class (it was in the original code).
    public abstract class SimpleWorkQueue
    {
        private readonly LinkedList<WaitCallback> mWorkQueue = [];
        private readonly int mMaxThreads;
        private int mActiveThreadCount = 0;
        private volatile bool mIsAborting = false;

        public SimpleWorkQueue()
        {
            mMaxThreads = Environment.ProcessorCount;
        }

        public SimpleWorkQueue(int maxThreads)
        {
            mMaxThreads = maxThreads;
        }

        public bool IsIdle => mActiveThreadCount == 0;

        public bool IsFullyActive => mActiveThreadCount == mMaxThreads;

        public bool IsSuspended { get; private set; } = false;

        public bool IsAborting => mIsAborting;

        // Adds work to the head of the work queue. Useful for workers that
        // want to reschedule themselves on suspend.
        public void AddFirst(WaitCallback work)
        {
            lock (mWorkQueue)
            {
                mWorkQueue.AddFirst(work);
                StartWorker();
            }
        }

        // Adds work to the tail of the work queue.
        public void AddLast(WaitCallback work)
        {
            lock (mWorkQueue)
            {
                mWorkQueue.AddLast(work);
                StartWorker();
            }
        }

        // Clears pending work without affecting active work.
        public void ClearPending()
        {
            lock (mWorkQueue)
            {
                mWorkQueue.Clear();
            }
        }

        // Stops processing of pending work.
        public void Suspend()
        {
            lock (mWorkQueue)
            {
                IsSuspended = true;
            }
        }

        // Resumes processing of pending work after being suspended.
        public void Resume()
        {
            lock (mWorkQueue)
            {
                IsSuspended = false;
                while (mActiveThreadCount < mWorkQueue.Count)
                {
                    StartWorker();
                }
            }
        }

        // Signals active workers to abort and clears pending work.
        public void Abort()
        {
            lock (mWorkQueue)
            {
                if (mActiveThreadCount > 0)
                {
                    // flag active workers to stop; last will reset the flag
                    mIsAborting = true;
                }

                // to avoid non-determinism, always clear the queue
                mWorkQueue.Clear();
            }
        }

        protected virtual void OnActive()
        {
        }

        protected virtual void OnIdle()
        {
            // auto-reset abort flag
            mIsAborting = false;
        }

        protected virtual void OnStart(WaitCallback work)
        {
        }

        protected virtual void OnStop(WaitCallback work)
        {
        }

        protected virtual void OnException(WaitCallback work, Exception e)
        {
        }

        // Assumes work queue lock is held.
        private void StartWorker()
        {
            if (mActiveThreadCount < mMaxThreads && !IsSuspended)
            {
                if (++mActiveThreadCount == 1)
                {
                    // hook for transition from Idle to Active
                    OnActive();
                }
                ThreadPool.QueueUserWorkItem(Worker);
            }
        }

        private void Worker(object? state)
        {
            while (true)
            {
                WaitCallback work;
                lock (mWorkQueue)
                {
                    LinkedListNode<WaitCallback>? head = mWorkQueue.First;
                    if (head == null || IsSuspended)
                    {
                        if (--mActiveThreadCount == 0)
                        {
                            // hook for transition from Active to Idle
                            OnIdle();
                        }
                        return;
                    }
                    work = head.Value;
                    mWorkQueue.RemoveFirst();
                }

                // hook for worker initialization
                OnStart(work);
                try
                {
                    work(work);
                }
                catch (Exception e)
                {
                    // hook for worker exceptions
                    OnException(work, e);
                }
                finally
                {
                    // hook for worker cleanup
                    OnStop(work);
                }
            }
        }
    };
}
