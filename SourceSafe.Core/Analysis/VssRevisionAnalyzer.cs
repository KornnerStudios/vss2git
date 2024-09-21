using System.Diagnostics;
using SourceSafe.Jobs;
using SourceSafe.Logical;
using SourceSafe.Logical.Actions;
using SourceSafe.Logical.Items;

namespace SourceSafe.Analysis
{
    /// <summary>
    /// Enumerates revisions in a VSS database.
    /// </summary>
    public sealed class VssRevisionAnalyzer : QueuedWorkerBase
    {
        public readonly record struct DeletedFileData(
            string ParentPhysicalName,
            string ItemPhysicalName
            );

        public VssDatabase Database { get; }

        private readonly List<VssProjectItem> mRootProjects = [];
        public IEnumerable<VssProjectItem> RootProjects => mRootProjects;

        public SortedDictionary<DateTime, ICollection<VssItemRevision>> SortedRevisions { get; } = [];
        public HashSet<string> ProcessedFiles { get; } = [];
        public HashSet<DeletedFileData> DestroyedFiles { get; } = [];

        private int mProjectCount;
        public int ProjectCount => Volatile.Read(ref mProjectCount);

        private int mFileCount;
        public int FileCount => Volatile.Read(ref mFileCount);

        private int mRevisionCount;
        public int RevisionCount => Volatile.Read(ref mRevisionCount);

        public VssRevisionAnalyzer(
            TrackedWorkQueue workQueue,
            IO.SimpleLogger logger,
            VssDatabase database)
            : base(workQueue, logger)
        {
            Database = database;
        }

        public bool IsDestroyed(string parentPhysicalName, string itemPhysicalName)
        {
            return DestroyedFiles.Contains(new DeletedFileData(parentPhysicalName, itemPhysicalName));
        }

        public void AddItem(VssProjectItem project)
        {
            if (project == null)
            {
                throw new ArgumentNullException(nameof(project));
            }
            else if (project.Database != Database)
            {
                throw new ArgumentException("Project database mismatch", nameof(project));
            }

            mRootProjects.Add(project);

            // #REVIEW: could the callback just use rootProjects.Last()?
            mWorkQueue.AddLast(workerCallback => AddItemWorkQueueCallback(workerCallback, project));
        }

        private void AddItemWorkQueueCallback(WorkerCallback workerCallback, VssProjectItem project)
        {
            mLogger.WriteSectionSeparator();
            LogStatus(workerCallback, "Building revision list");

            mLogger.WriteLine($"Root project: {project.LogicalPath}");

            var stopwatch = Stopwatch.StartNew();
            VssAnalysisUtils.RecurseItems(project,
                (VssProjectItem subproject) =>
                {
                    if (mWorkQueue.IsAborting)
                    {
                        return AnalysisRecursionStatus.Abort;
                    }

                    ProcessItem(subproject);
                    ++mProjectCount;
                    return AnalysisRecursionStatus.Continue;
                },
                (VssProjectItem subproject, VssFileItem file) =>
                {
                    if (mWorkQueue.IsAborting)
                    {
                        return AnalysisRecursionStatus.Abort;
                    }

                    // only process shared files once (projects are never shared)
                    if (!ProcessedFiles.Contains(file.PhysicalName))
                    {
                        ProcessedFiles.Add(file.PhysicalName);
                        ProcessItem(file);
                        ++mFileCount;
                    }
                    return AnalysisRecursionStatus.Continue;
                });
            stopwatch.Stop();

            mLogger.WriteSectionSeparator();
            mLogger.WriteLine("Analysis complete in {0:HH:mm:ss}", new DateTime(stopwatch.ElapsedTicks));
            mLogger.WriteLine($"Revisions: {mRevisionCount}");
        }

        private void ProcessItem(VssItemBase item)
        {
            try
            {
                foreach (VssItemRevisionBase vssRevision in item.Revisions)
                {
                    VssActionType actionType = vssRevision.Action.Type;
                    if (vssRevision.Action is VssNamedActionBase namedAction)
                    {
                        var tuple = new DeletedFileData(item.PhysicalName, namedAction.Name.PhysicalName);

                        if (actionType == VssActionType.Destroy)
                        {
                            // https://msdn.microsoft.com/en-us/library/b3d0xbb5(v=vs.80).aspx

                            // This command is available through the Destroy Permanently check box
                            // available in the Delete dialog box. When you select this check box,
                            // you remove the selected file or project from the database permanently,
                            // and destroy any related history. You cannot recover an item that you
                            // have destroyed. You must have the Destroy project right to use this command.

                            // track destroyed files so missing history can be anticipated
                            // (note that Destroy actions on shared files simply delete
                            // that copy, so destroyed files can't be completely ignored)
                            DestroyedFiles.Add(tuple);
                        }
                        else if (actionType == VssActionType.Share || actionType == VssActionType.Branch)
                        {
                            bool wasRemoved = DestroyedFiles.Remove(tuple);
                            SourceSafeConstants.MarkUnusedVariable(ref wasRemoved);
                        }
                    }

                    var revision = new VssItemRevision(vssRevision.DateTime,
                        vssRevision.UserName!, item.ItemName, vssRevision.Version,
                        vssRevision.Comment ?? "", vssRevision.Action);

                    if (!SortedRevisions.TryGetValue(vssRevision.DateTime, out ICollection<VssItemRevision>? revisionSet))
                    {
                        revisionSet = new List<VssItemRevision>();
                        SortedRevisions[vssRevision.DateTime] = revisionSet;
                    }
                    revisionSet.Add(revision);
                    ++mRevisionCount;
                }
            }
            catch (Physical.Records.RecordExceptionBase e)
            {
                string message = $"Failed to read revisions for ({item.PhysicalName}): {Exceptions.ExceptionFormatter.Format(e)}";
                LogException(e, message);
                ReportError(message);
            }
        }
    };
}
