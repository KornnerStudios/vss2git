/* Copyright 2009 HPDI, LLC
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using SourceSafe.Logical;
using SourceSafe.Logical.Actions;
using SourceSafe.Logical.Items;

namespace Hpdi.Vss2Git
{
    /// <summary>
    /// Enumerates revisions in a VSS database.
    /// </summary>
    /// <author>Trevor Robinson</author>
    sealed class RevisionAnalyzer : Worker
    {
        public readonly record struct DeletedFileData(
            string ParentPhysicalName,
            string ItemPhysicalName
            );

        public VssDatabase Database { get; }

        private readonly List<VssProjectItem> mRootProjects = [];
        public IEnumerable<VssProjectItem> RootProjects => mRootProjects;

        public SortedDictionary<DateTime, ICollection<Revision>> SortedRevisions { get; } = [];
        public HashSet<string> ProcessedFiles { get; } = [];
        public HashSet<DeletedFileData> DestroyedFiles { get; } = [];

        private int mProjectCount;
        public int ProjectCount => Thread.VolatileRead(ref mProjectCount);

        private int mFileCount;
        public int FileCount => Thread.VolatileRead(ref mFileCount);

        private int mRevisionCount;
        public int RevisionCount => Thread.VolatileRead(ref mRevisionCount);

        public RevisionAnalyzer(WorkQueue workQueue, SourceSafe.IO.SimpleLogger logger, VssDatabase database)
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
            workQueue.AddLast(work => AddItemWorkQueueCallback(work, project));
        }

        private void AddItemWorkQueueCallback(object work, VssProjectItem project)
        {
            logger.WriteSectionSeparator();
            LogStatus(work, "Building revision list");

            logger.WriteLine($"Root project: {project.LogicalPath}");

            var stopwatch = Stopwatch.StartNew();
            VssUtil.RecurseItems(project,
                (VssProjectItem subproject) =>
                {
                    if (workQueue.IsAborting)
                    {
                        return RecursionStatus.Abort;
                    }

                    ProcessItem(subproject);
                    ++mProjectCount;
                    return RecursionStatus.Continue;
                },
                (VssProjectItem subproject, VssFileItem file) =>
                {
                    if (workQueue.IsAborting)
                    {
                        return RecursionStatus.Abort;
                    }

                    // only process shared files once (projects are never shared)
                    if (!ProcessedFiles.Contains(file.PhysicalName))
                    {
                        ProcessedFiles.Add(file.PhysicalName);
                        ProcessItem(file);
                        ++mFileCount;
                    }
                    return RecursionStatus.Continue;
                });
            stopwatch.Stop();

            logger.WriteSectionSeparator();
            logger.WriteLine("Analysis complete in {0:HH:mm:ss}", new DateTime(stopwatch.ElapsedTicks));
            logger.WriteLine($"Revisions: {mRevisionCount}");
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
                            VssUtil.MarkUnusedVariable(ref wasRemoved);
                        }
                    }

                    var revision = new Revision(vssRevision.DateTime,
                        vssRevision.User, item.ItemName, vssRevision.Version,
                        vssRevision.Comment, vssRevision.Action);

                    if (!SortedRevisions.TryGetValue(vssRevision.DateTime, out ICollection<Revision> revisionSet))
                    {
                        revisionSet = new List<Revision>();
                        SortedRevisions[vssRevision.DateTime] = revisionSet;
                    }
                    revisionSet.Add(revision);
                    ++mRevisionCount;
                }
            }
            catch (SourceSafe.Physical.Records.RecordExceptionBase e)
            {
                string message = $"Failed to read revisions for ({item.PhysicalName}): {SourceSafe.Exceptions.ExceptionFormatter.Format(e)}";
                LogException(e, message);
                ReportError(message);
            }
        }
    }
}
