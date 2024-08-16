using System.Diagnostics;
using SourceSafe.Jobs;
using SourceSafe.Logical.Actions;

namespace SourceSafe.Analysis
{
    /// <summary>
    /// Reconstructs changesets from independent revisions.
    /// </summary>
    public sealed class PseudoChangesetBuilder : QueuedWorkerBase
    {
        private readonly VssRevisionAnalyzer revisionAnalyzer;

        public List<PseudoChangeset> Changesets { get; } = [];
        public List<PseudoChangeset> ChangesetsWithMeaningfulComments { get; } = [];
        public TimeSpan AnyCommentThreshold { get; set; } = TimeSpan.FromSeconds(30);
        public TimeSpan SameCommentThreshold { get; set; } = TimeSpan.FromMinutes(10);

        public PseudoChangesetBuilder(
            TrackedWorkQueue workQueue,
            IO.SimpleLogger logger,
            VssRevisionAnalyzer revisionAnalyzer)
            : base(workQueue, logger)
        {
            this.revisionAnalyzer = revisionAnalyzer;
        }

        /// <summary>
        /// For handling the case where there's an earlier Delete+Destroy but then in the same commit something tries to rename a file to what was destroyed
        /// </summary>
        public bool ForceFlushRenameAfterDeleteForFiles { get; set; } = true;
        public void BuildChangesets()
        {
            mWorkQueue.AddLast(BuildChangesetsWorkQueueCallback);
        }
        private void BuildChangesetsWorkQueueCallback(WorkerCallback thisMethodAsCallback)
        {
            mLogger.WriteSectionSeparator();
            LogStatus(thisMethodAsCallback, "Building changesets");
            mLogger.WriteLine($"\tAnyCommentThreshold: {AnyCommentThreshold.TotalSeconds} seconds");
            mLogger.WriteLine($"\tSameCommentThreshold: {SameCommentThreshold.TotalSeconds} seconds");
            mLogger.WriteLine();

            var stopwatch = Stopwatch.StartNew();
            var pendingChangesByUser = new Dictionary<string, PseudoChangeset>();
            bool hasDelete = false;
            bool hasRename = false;
            string changesetReason = "";

            foreach (KeyValuePair<DateTime, ICollection<VssItemRevision>> dateEntry in revisionAnalyzer.SortedRevisions)
            {
                foreach (VssItemRevision revision in dateEntry.Value)
                {
                    // determine target of project revisions
                    VssActionType actionType = revision.Action.Type;
                    var namedAction = revision.Action as VssNamedActionBase;
                    string targetFile = revision.Item.PhysicalName;
                    if (namedAction != null)
                    {
                        targetFile = namedAction.Name.PhysicalName;
                    }

                    // Create actions are only used to obtain initial item comments;
                    // items are actually created when added to a project
                    bool creating = (actionType == VssActionType.Create ||
                        (actionType == VssActionType.Branch && !revision.Item.IsProject));

                    // Share actions are never conflict (which is important,
                    // since Share always precedes Branch)
                    bool nonconflicting = creating || (actionType == VssActionType.Share) || (actionType == VssActionType.MoveFrom) || (actionType == VssActionType.MoveTo);

                    // look up the pending change for user of this revision
                    // and flush changes past time threshold
                    string pendingUser = revision.User;
                    PseudoChangeset? pendingChange = null;
                    List<string>? flushedUsers = null;
                    foreach (KeyValuePair<string, PseudoChangeset> userEntry in pendingChangesByUser)
                    {
                        string user = userEntry.Key;
                        PseudoChangeset change = userEntry.Value;

                        // flush change if file conflict or past time threshold
                        bool flush = false;
                        TimeSpan timeDiff = revision.DateTime - change.DateTime;
                        if (AnyCommentThreshold > TimeSpan.Zero &&
                            timeDiff > AnyCommentThreshold)
                        {
                            if (SameCommentThreshold > TimeSpan.Zero &&
                                VssItemRevision.HaveSameComment(revision, change.Revisions.Last()))
                            {
                                string message;
                                if (timeDiff < SameCommentThreshold)
                                {
                                    message = "Using same-comment threshold";
                                }
                                else
                                {
                                    message = "Same comment but exceeded threshold";
                                    flush = true;
                                }
                                mLogger.WriteLine("NOTE: {0} ({1} second gap):",
                                    message, timeDiff.TotalSeconds);
                            }
                            else
                            {
                                flush = true;
                            }

                            if (flush)
                            {
                                changesetReason = $"Time difference {revision.DateTime} - {change.DateTime} ({timeDiff} sec)";
                            }
                        }
                        else if (!nonconflicting && change.ContainsTargetFile(targetFile))
                        {
                            // (target)@version format matches DumpChangeset output
                            changesetReason = $"File conflict on ({targetFile})@{revision.Version}";
                            flush = true;
                        }
                        else if (hasDelete && actionType == VssActionType.Rename)
                        {
                            if (revision.Action is VssRenameAction renameAction && (renameAction.Name.IsProject || ForceFlushRenameAfterDeleteForFiles))
                            {
                                // split the change set if a rename of a directory follows a delete
                                // otherwise a git error occurs
                                changesetReason = $"Splitting changeset due to rename after delete in ({targetFile})";
                                flush = true;
                            }
                        }
                        else if (hasRename && (actionType == VssActionType.Delete || actionType == VssActionType.Destroy))
                        {
                            if (namedAction != null)
                            {
                                // split the change set if a rename of a directory follows a delete
                                // otherwise a git error occurs
                                changesetReason = $"Splitting changeset due to delete after rename in ({targetFile})";
                                flush = true;
                            }
                        }

                        if (flush)
                        {
                            AddChangeset(change, changesetReason);
                            if (flushedUsers == null)
                            {
                                flushedUsers = [];
                            }
                            flushedUsers.Add(user);
                            hasDelete = false;
                            hasRename = false;
                        }
                        else if (user == pendingUser)
                        {
                            pendingChange = change;
                        }
                    }
                    if (flushedUsers != null)
                    {
                        foreach (string user in flushedUsers)
                        {
                            pendingChangesByUser.Remove(user);
                        }
                    }

                    // if no pending change for user, create a new one
                    if (pendingChange == null)
                    {
                        pendingChange = new PseudoChangeset
                        {
                            User = pendingUser,
                        };
                        pendingChangesByUser[pendingUser] = pendingChange;
                    }

                    // update the time of the change based on the last revision
                    pendingChange.DateTime = revision.DateTime;

                    // add the revision to the change
                    pendingChange.Revisions.Add(revision);
                    hasDelete |= actionType == VssActionType.Delete || actionType == VssActionType.Destroy;
                    hasRename |= actionType == VssActionType.Rename;

                    // track target files in changeset to detect conflicting actions
                    if (!nonconflicting)
                    {
                        pendingChange.AddTargetFile(targetFile, actionType);
                    }

                    // build up a concatenation of unique revision comments
                    string revComment = revision.Comment;
                    if (revComment != null)
                    {
                        revComment = revComment.Trim();

                        if (revComment.Length > 0 && (0 == pendingChange.Comment.Count || !pendingChange.Comment.Contains(revComment)))
                        {
                            pendingChange.Comment.Add(revComment);
                        }
                    }
                }
            }

            // flush all remaining changes
            foreach (PseudoChangeset change in pendingChangesByUser.Values)
            {
                AddChangeset(change, "Remaining revision(s)");
            }
            stopwatch.Stop();

            mLogger.WriteSectionSeparator();
            mLogger.WriteLine($"Found {Changesets.Count} changesets in {stopwatch.Elapsed}");
        }

        private void AddChangeset(
            PseudoChangeset change,
            string reason)
        {
            change.Id = Changesets.Count + 1;
            Changesets.Add(change);
            DumpChangeset(change, change.Id, 0, reason);
        }

        private void DumpChangeset(
            PseudoChangeset changeset,
            int changesetId,
            int indent,
            string reason)
        {
            string indentStr = IO.OutputUtil.GetIndentString(indent);

            DateTime firstRevTime = changeset.Revisions.First().DateTime;
            TimeSpan changeDuration = changeset.DateTime - firstRevTime;

            mLogger.WriteLine("{0}Changeset {1} - {2} ({3} secs) {4} {5} file(s)",
                indentStr, changesetId, changeset.DateTime.ToIsoTimestamp(), changeDuration.TotalSeconds,
                changeset.User, changeset.Revisions.Count);

            if (changeset.Comment.Count > 0)
            {
                ChangesetsWithMeaningfulComments.Add(changeset);
            }

            foreach (string line in changeset.Comment)
            {
                mLogger.WriteLine("{0}{1}", indentStr, line);
            }

            mLogger.WriteLine();
            foreach (VssItemRevision revision in changeset.Revisions)
            {
                // (target)@version format matches "File conflict..." output
                mLogger.WriteLine("{0}  {1} {2}@{3} {4}",
                    indentStr, revision.DateTime.ToIsoTimestamp(), revision.Item, revision.Version, revision.Action);
            }

            mLogger.WriteLine("{0}//------------------------- {1} {2}//",
                indentStr,
                reason,
                53 > reason.Length
                    ? new string('-', 53 - reason.Length)
                    : "");
        }
    };
}
