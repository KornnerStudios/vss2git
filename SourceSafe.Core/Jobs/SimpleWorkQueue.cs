
namespace SourceSafe.Jobs
{
    public delegate void WorkerCallback(WorkerCallback self);

    /// <summary>
    /// Simple work queue over a bounded number of thread-pool threads.
    /// </summary>
    // This is abstract only because nothing instances it, but it could be a concrete class (it was in the original code).
    public abstract class SimpleWorkQueue
    {
        private readonly LinkedList<WorkerCallback> mWorkLinkedList = [];
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
        public void AddFirst(WorkerCallback work)
        {
            lock (mWorkLinkedList)
            {
                mWorkLinkedList.AddFirst(work);
                StartWorker();
            }
        }

        // Adds work to the tail of the work queue.
        public void AddLast(WorkerCallback work)
        {
            lock (mWorkLinkedList)
            {
                mWorkLinkedList.AddLast(work);
                StartWorker();
            }
        }

        // Clears pending work without affecting active work.
        public void ClearPending()
        {
            lock (mWorkLinkedList)
            {
                mWorkLinkedList.Clear();
            }
        }

        // Stops processing of pending work.
        public void Suspend()
        {
            lock (mWorkLinkedList)
            {
                IsSuspended = true;
            }
        }

        // Resumes processing of pending work after being suspended.
        public void Resume()
        {
            lock (mWorkLinkedList)
            {
                IsSuspended = false;
                while (mActiveThreadCount < mWorkLinkedList.Count)
                {
                    StartWorker();
                }
            }
        }

        // Signals active workers to abort and clears pending work.
        public void Abort()
        {
            lock (mWorkLinkedList)
            {
                if (mActiveThreadCount > 0)
                {
                    // flag active workers to stop; last will reset the flag
                    mIsAborting = true;
                }

                // to avoid non-determinism, always clear the queue
                mWorkLinkedList.Clear();
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

        protected virtual void OnStart(WorkerCallback work)
        {
        }

        protected virtual void OnStop(WorkerCallback work)
        {
        }

        protected virtual void OnException(WorkerCallback work, Exception e)
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

        private void Worker(object? _)
        {
            while (true)
            {
                WorkerCallback work;
                lock (mWorkLinkedList)
                {
                    LinkedListNode<WorkerCallback>? head = mWorkLinkedList.First;
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
                    mWorkLinkedList.RemoveFirst();
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
