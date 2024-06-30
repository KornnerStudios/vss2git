/* Copyright 2017, Trapeze Poland sp. z o.o.
 *
 * Author: Dariusz Bywalec
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

using Hpdi.VssLogicalLib;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Hpdi.Vss2Git.GitActions
{
    /// <summary>
    /// Represents a git commit action.
    /// </summary>
    /// <author>Dariusz Bywalec</author>
    class Commit : BranchAction
    {
        private const string DefaultComment = "Vss2Git";
        private static readonly char[] charsToTrim = { '$', '/', '\\' };

        private readonly Changeset changeset;
        private readonly string authorName;
        private readonly string authorEmail;
        private readonly DateTime utcTime;
        private readonly bool includeVssMetaDataInComments;
        private bool needsCommit = false;

        private List<Revision> revisions = new List<Revision>();
        private List<IGitAction> tags = new List<IGitAction>();
        private List<IGitAction> actions = new List<IGitAction>();

        List<string> addedFiles = new List<string>();
        List<string> movedFiles = new List<string>();
        List<string> deletedFiles = new List<string>();

        public Commit(Changeset changeset, string authorName, string authorEmail, DateTime utcTime, bool includeVssMetaDataInComments)
        {
            this.changeset = changeset;
            this.authorName = authorName;
            this.authorEmail = authorEmail;
            this.utcTime = utcTime;
            this.includeVssMetaDataInComments = includeVssMetaDataInComments;
        }

        public override bool Run(Logger logger, IGitWrapper git, IGitStatistic stat)
        {
            bool result = false;

            foreach (IGitAction a in actions)
            {
                a.Run(logger, git, stat);
            }

            if (git.NeedsCommit())
            {
                List<string> message = BuildCommitMessage(false);

                logger.WriteLine("Creating commit: {0}", message.FirstOrDefault());

                try
                {
                    result = git.AddAll() &&
                             git.Commit(authorName, authorEmail, String.Join("\n", message), utcTime);

                    stat.AddCommit();
                }
                catch (LibGit2Sharp.EmptyCommitException e)
                {
                    result = true;
                    logger.WriteLine("NOTE: Ignoring empty commit: {0}", e.Message);
                }
            }

            foreach (IGitAction t in tags)
            {
                t.Run(logger, git, stat);
            }

            return result;
        }

        public void AddTag(Revision revision, CreateTag tag)
        {
            revisions.Add(revision);
            tags.Add(tag);
        }

        public void AddAction(Revision revision, IGitAction action, bool needsCommit)
        {
            revisions.Add(revision);
            actions.Add(action);
            this.needsCommit |= needsCommit;
        }
        public void AddFile(Revision revision, WriteFile action, string message)
        {
            AddAction(revision, action, true);

            message = message.Trim(charsToTrim);

            if (!addedFiles.Contains(message))
            {
                addedFiles.Add(message);
            }
        }
        public void WriteFile(Revision revision, WriteFile action)
        {
            AddAction(revision, action, true);
        }
        public void MoveFileOrDirectory(Revision revision, IGitAction action, string message)
        {
            AddAction(revision, action, true);

            message = message.Trim(charsToTrim);

            if (!movedFiles.Contains(message))
            {
                movedFiles.Add(message);
            }
        }
        public void DeleteFileOrDirectory(Revision revision, IGitAction action, string message)
        {
            AddAction(revision, action, true);

            message = message.Trim(charsToTrim);

            if (!deletedFiles.Contains(message))
            {
                deletedFiles.Add(message);
            }
        }

        private List<string> BuildCommitMessage(bool firstLineOnly)
        {
            List<string> message = new List<string>();

            if (0 < changeset.Comment.Count)
            {
                message.AddRange(changeset.Comment);
            }
            else
            {
                addedFiles.Sort();
                movedFiles.Sort();
                deletedFiles.Sort();

                if (0 < addedFiles.Count && 0 < movedFiles.Count && 0 < deletedFiles.Count)
                {
                    message.Add("Vss2Git: Add/Move/Delete file(s)");

                    message.Add("");
                    message.Add("Added file(s):");
                    message.AddRange(addedFiles);

                    message.Add("");
                    message.Add("Moved file(s):");
                    message.AddRange(movedFiles);

                    message.Add("");
                    message.Add("Deleted file(s):");
                    message.AddRange(deletedFiles);
                }
                else if (0 < addedFiles.Count && 0 < movedFiles.Count)
                {
                    message.Add("Vss2Git: Add/Move file(s)");

                    message.Add("");
                    message.Add("Added file(s):");
                    message.AddRange(addedFiles);

                    message.Add("");
                    message.Add("Moved file(s):");
                    message.AddRange(movedFiles);
                }
                else if (0 < addedFiles.Count && 0 < deletedFiles.Count)
                {
                    message.Add("Vss2Git: Add/Delete file(s)");

                    message.Add("");
                    message.Add("Added file(s):");
                    message.AddRange(addedFiles);

                    message.Add("");
                    message.Add("Deleted file(s):");
                    message.AddRange(deletedFiles);
                }
                else if (0 < movedFiles.Count && 0 < deletedFiles.Count)
                {
                    message.Add("Vss2Git: Move/Delete files");

                    message.Add("");
                    message.Add("Moved file(s):");
                    message.AddRange(movedFiles);

                    message.Add("");
                    message.Add("Deleted file(s):");
                    message.AddRange(deletedFiles);
                }
                else if (0 < addedFiles.Count)
                {
                    message.Add("Vss2Git: Add file(s)");

                    message.Add("");
                    message.Add("Added file(s):");
                    message.AddRange(addedFiles);
                }
                else if (0 < movedFiles.Count)
                {
                    message.Add("Vss2Git: Move/Rename file(s)");

                    message.Add("");
                    message.Add("Moved file(s):");
                    message.AddRange(movedFiles);
                }
                else if (0 < deletedFiles.Count)
                {
                    message.Add("Vss2Git: Delete/Destroy file(s)");

                    message.Add("");
                    message.Add("Deleted file(s):");
                    message.AddRange(deletedFiles);
                }
            }

            if (0 == message.Count)
            {
                message.Add(DefaultComment);
            }

            if (includeVssMetaDataInComments && 0 < revisions.Count)
            {
                message.Add("");

                string indentStr = "";

                DateTime firstRevTime = revisions.First().DateTime;
                TimeSpan changeDuration = changeset.DateTime - firstRevTime;

                message.Add(String.Format("{0}Changeset {1} - {2} ({3} secs){4} {5} {6} file(s)",
                    indentStr, changeset.Id, VssDatabase.FormatISOTimestamp(changeset.DateTime), changeDuration.TotalSeconds,
                    "", changeset.User, changeset.Revisions.Count));

                foreach (Revision revision in revisions)
                {
                    message.Add(String.Format("{0}  {1} {2}@{3} {4}", indentStr, VssDatabase.FormatISOTimestamp(revision.DateTime),
                                                                      revision.Item, revision.Version, revision.Action));
                }
            }

#if false // leave the formatting to the caller, e.g. trim to the first message only in the logger
            if (firstLineOnly && 0 < message.Count)
            {
                return message[0];
            }
            else
            {
                return String.Join("\n", message);
            }
#else
            return message;
#endif
        }
    }
}
