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
using Hpdi.VssLogicalLib;
using System.Linq;

namespace Hpdi.Vss2Git
{
    /// <summary>
    /// Reconstructs changesets from independent revisions.
    /// </summary>
    /// <author>Trevor Robinson</author>
    class ChangesetBuilder : Worker
    {
        private readonly RevisionAnalyzer revisionAnalyzer;

        private readonly LinkedList<Changeset> changesets = new LinkedList<Changeset>();
        public LinkedList<Changeset> Changesets
        {
            get { return changesets; }
        }

        private TimeSpan anyCommentThreshold = TimeSpan.FromSeconds(30);
        public TimeSpan AnyCommentThreshold
        {
            get { return anyCommentThreshold; }
            set { anyCommentThreshold = value; }
        }


        private TimeSpan sameCommentThreshold = TimeSpan.FromMinutes(10);
        public TimeSpan SameCommentThreshold
        {
            get { return sameCommentThreshold; }
            set { sameCommentThreshold = value; }
        }

        public ChangesetBuilder(WorkQueue workQueue, Logger logger, RevisionAnalyzer revisionAnalyzer)
            : base(workQueue, logger)
        {
            this.revisionAnalyzer = revisionAnalyzer;
        }

        public void BuildChangesets()
        {
            workQueue.AddLast(delegate(object work)
            {
                logger.WriteSectionSeparator();
                LogStatus(work, "Building changesets");

                var stopwatch = Stopwatch.StartNew();
                var pendingChangesByUser = new Dictionary<string, Changeset>();
                var hasDelete = false;
                var hasRename = false;
                var changesetReason = "";

                foreach (var dateEntry in revisionAnalyzer.SortedRevisions)
                {
                    foreach (Revision revision in dateEntry.Value)
                    {
                        // determine target of project revisions
                        var actionType = revision.Action.Type;
                        var namedAction = revision.Action as VssNamedAction;
                        var targetFile = revision.Item.PhysicalName;
                        if (namedAction != null)
                        {
                            targetFile = namedAction.Name.PhysicalName;
                        }

                        // Create actions are only used to obtain initial item comments;
                        // items are actually created when added to a project
                        var creating = (actionType == VssActionType.Create ||
                            (actionType == VssActionType.Branch && !revision.Item.IsProject));

                        // Share actions are never conflict (which is important,
                        // since Share always precedes Branch)
                        var nonconflicting = creating || (actionType == VssActionType.Share);

                        // look up the pending change for user of this revision
                        // and flush changes past time threshold
                        var pendingUser = revision.User;
                        Changeset pendingChange = null;
                        LinkedList<string> flushedUsers = null;
                        foreach (var userEntry in pendingChangesByUser)
                        {
                            var user = userEntry.Key;
                            var change = userEntry.Value;

                            // flush change if file conflict or past time threshold
                            var flush = false;
                            var timeDiff = revision.DateTime - change.DateTime;
                            if (timeDiff > anyCommentThreshold)
                            {
                                if (HasSameComment(revision, change.Revisions.Last()))
                                {
                                    string message;
                                    if (timeDiff < sameCommentThreshold)
                                    {
                                        message = "Using same-comment threshold";
                                    }
                                    else
                                    {
                                        message = "Same comment but exceeded threshold";
                                        flush = true;
                                    }
                                    logger.WriteLine("NOTE: {0} ({1} second gap):",
                                        message, timeDiff.TotalSeconds);
                                }
                                else
                                {
                                    flush = true;
                                }

                                if (flush)
                                {
                                    changesetReason = String.Format("Time difference {0} - {1} ({2} sec)", revision.DateTime, change.DateTime, timeDiff);
                                }
                            }
                            else if (!nonconflicting && change.TargetFiles.Contains(targetFile))
                            {
                                changesetReason = String.Format("File conflict on ({0})", targetFile);
                                flush = true;
                            }
                            else if (hasDelete && actionType == VssActionType.Rename)
                            {
                                var renameAction = revision.Action as VssRenameAction;
                                if (renameAction != null && renameAction.Name.IsProject)
                                {
                                    // split the change set if a rename of a directory follows a delete
                                    // otherwise a git error occurs
                                    changesetReason = String.Format("Splitting changeset due to rename after delete in ({0})", targetFile);
                                    flush = true;
                                }
                            }
                            else if (hasRename && (actionType == VssActionType.Delete || actionType == VssActionType.Destroy))
                            {
                                if (namedAction != null)
                                {
                                    // split the change set if a rename of a directory follows a delete
                                    // otherwise a git error occurs
                                    changesetReason = String.Format("Splitting changeset due to delete after rename in ({0})", targetFile);
                                    flush = true;
                                }
                            }

                            if (flush)
                            {
                                AddChangeset(change, changesetReason);
                                if (flushedUsers == null)
                                {
                                    flushedUsers = new LinkedList<string>();
                                }
                                flushedUsers.AddLast(user);
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
                            pendingChange = new Changeset();
                            pendingChange.User = pendingUser;
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
                            pendingChange.TargetFiles.Add(targetFile);
                        }

                        // build up a concatenation of unique revision comments
                        var revComment = revision.Comment;
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
                foreach (var change in pendingChangesByUser.Values)
                {
                    AddChangeset(change, "Remaining revisions");
                }
                stopwatch.Stop();

                logger.WriteSectionSeparator();
                logger.WriteLine("Found {0} changesets in {1:HH:mm:ss}",
                    changesets.Count, new DateTime(stopwatch.ElapsedTicks));
            });
        }

        private bool HasSameComment(Revision rev1, Revision rev2)
        {
            return (!string.IsNullOrEmpty(rev1.Comment) && !string.IsNullOrEmpty(rev1.Comment) && rev1.Comment == rev2.Comment);
        }

        private void AddChangeset(Changeset change, string reason)
        {
            change.Id = changesets.Count + 1;
            changesets.AddLast(change);
            DumpChangeset(change, change.Id, 0, reason);
        }

        private void DumpChangeset(Changeset changeset, int changesetId, int indent, string reason)
        {
            var indentStr = new string(' ', indent);

            var firstRevTime = changeset.Revisions.First().DateTime;
            var changeDuration = changeset.DateTime - firstRevTime;

            logger.WriteLine("{0}Changeset {1} - {2} ({3} secs) {4} {5} file(s)",
                indentStr, changesetId, VssDatabase.FormatISOTimestamp(changeset.DateTime), changeDuration.TotalSeconds,
                changeset.User, changeset.Revisions.Count);

            foreach (var line in changeset.Comment)
            {
                logger.WriteLine("{0}{1}", indentStr, line);
            }

            logger.WriteLine();
            foreach (var revision in changeset.Revisions)
            {
                logger.WriteLine("{0}  {1} {2}@{3} {4}", indentStr, VssDatabase.FormatISOTimestamp(revision.DateTime), revision.Item, revision.Version, revision.Action);
            }

            logger.WriteLine("{0}//------------------------- {1} {2}//", indentStr, reason, 53 > reason.Length ? new string('-', 53 - reason.Length) : "");
        }
    }
}
