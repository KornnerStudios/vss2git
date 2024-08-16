using SourceSafe.Analysis;

namespace SourceSafe.GitConversion.GitActions
{
    /// <summary>
    /// Represents a git commit action.
    /// </summary>
    sealed class CommitAction : BranchActionBase
    {
        private const string DefaultComment = "Vss2Git";
        private static readonly char[] CharsToTrim = ['$', '/', '\\'];

        private readonly PseudoChangeset mChangeset;
        private readonly string mAuthorName;
        private readonly string mAuthorEmail;
        private readonly DateTime mUtcTime;
        private readonly bool mIncludeVssMetaDataInComments;
        // #REVIEW this is never read
        private bool mNeedsCommit = false;

        private readonly List<VssItemRevision> mRevisions = [];
        private readonly List<IGitAction> mTags = [];
        private readonly List<IGitAction> mActions = [];

        private readonly List<string> mAddedFiles = [];
        private readonly List<string> mMovedFiles = [];
        private readonly List<string> mDeletedFiles = [];

        public CommitAction(
            PseudoChangeset changeset,
            string authorName,
            string authorEmail,
            DateTime utcTime,
            bool includeVssMetaDataInComments)
        {
            mChangeset = changeset;
            mAuthorName = authorName;
            mAuthorEmail = authorEmail;
            mUtcTime = utcTime;
            mIncludeVssMetaDataInComments = includeVssMetaDataInComments;
        }

        public override bool Run(
            IO.SimpleLogger logger,
            IGitWrapper git,
            IGitStatistic stat)
        {
            bool result = false;

            foreach (IGitAction a in mActions)
            {
                a.Run(logger, git, stat);
            }

            if (git.NeedsCommit())
            {
                List<string> message = BuildCommitMessage(false);

                logger.WriteLine("Creating commit: {0}", message.FirstOrDefault() ?? "<NO-COMMIT-MESSAGE>");

                try
                {
                    result = git.AddAll() &&
                             git.Commit(mAuthorName, mAuthorEmail, String.Join("\n", message), mUtcTime);

                    stat.AddCommit();
                }
                catch (LibGit2Sharp.EmptyCommitException e)
                {
                    result = true;
                    logger.WriteLine("NOTE: Ignoring empty commit: {0}", e.Message);
                }
            }

            foreach (IGitAction t in mTags)
            {
                t.Run(logger, git, stat);
            }

            return result;
        }

        public void AddTag(
            VssItemRevision revision,
            CreateTagAction tag)
        {
            mRevisions.Add(revision);
            mTags.Add(tag);
        }

        public void AddAction(
            VssItemRevision revision,
            IGitAction action,
            bool needsCommit)
        {
            mRevisions.Add(revision);
            mActions.Add(action);
            this.mNeedsCommit |= needsCommit;
        }
        public void AddFile(
            VssItemRevision revision,
            WriteFileAction action,
            string message)
        {
            AddAction(revision, action, true);

            message = message.Trim(CharsToTrim);

            if (!mAddedFiles.Contains(message))
            {
                mAddedFiles.Add(message);
            }
        }
        public void WriteFile(
            VssItemRevision revision,
            WriteFileAction action)
        {
            AddAction(revision, action, true);
        }
        public void MoveFileOrDirectory(
            VssItemRevision revision,
            IGitAction action,
            string message)
        {
            AddAction(revision, action, true);

            message = message.Trim(CharsToTrim);

            if (!mMovedFiles.Contains(message))
            {
                mMovedFiles.Add(message);
            }
        }
        public void DeleteFileOrDirectory(
            VssItemRevision revision,
            IGitAction action,
            string message)
        {
            AddAction(revision, action, true);

            message = message.Trim(CharsToTrim);

            if (!mDeletedFiles.Contains(message))
            {
                mDeletedFiles.Add(message);
            }
        }

        private List<string> BuildCommitMessage(
            bool firstLineOnly)
        {
            List<string> message = [];

            if (0 < mChangeset.Comment.Count)
            {
                message.AddRange(mChangeset.Comment);
            }
            else
            {
                mAddedFiles.Sort();
                mMovedFiles.Sort();
                mDeletedFiles.Sort();

                if (0 < mAddedFiles.Count && 0 < mMovedFiles.Count && 0 < mDeletedFiles.Count)
                {
                    message.Add("Vss2Git: Add/Move/Delete file(s)");

                    message.Add("");
                    message.Add("Added file(s):");
                    message.AddRange(mAddedFiles);

                    message.Add("");
                    message.Add("Moved file(s):");
                    message.AddRange(mMovedFiles);

                    message.Add("");
                    message.Add("Deleted file(s):");
                    message.AddRange(mDeletedFiles);
                }
                else if (0 < mAddedFiles.Count && 0 < mMovedFiles.Count)
                {
                    message.Add("Vss2Git: Add/Move file(s)");

                    message.Add("");
                    message.Add("Added file(s):");
                    message.AddRange(mAddedFiles);

                    message.Add("");
                    message.Add("Moved file(s):");
                    message.AddRange(mMovedFiles);
                }
                else if (0 < mAddedFiles.Count && 0 < mDeletedFiles.Count)
                {
                    message.Add("Vss2Git: Add/Delete file(s)");

                    message.Add("");
                    message.Add("Added file(s):");
                    message.AddRange(mAddedFiles);

                    message.Add("");
                    message.Add("Deleted file(s):");
                    message.AddRange(mDeletedFiles);
                }
                else if (0 < mMovedFiles.Count && 0 < mDeletedFiles.Count)
                {
                    message.Add("Vss2Git: Move/Delete files");

                    message.Add("");
                    message.Add("Moved file(s):");
                    message.AddRange(mMovedFiles);

                    message.Add("");
                    message.Add("Deleted file(s):");
                    message.AddRange(mDeletedFiles);
                }
                else if (0 < mAddedFiles.Count)
                {
                    message.Add("Vss2Git: Add file(s)");

                    message.Add("");
                    message.Add("Added file(s):");
                    message.AddRange(mAddedFiles);
                }
                else if (0 < mMovedFiles.Count)
                {
                    message.Add("Vss2Git: Move/Rename file(s)");

                    message.Add("");
                    message.Add("Moved file(s):");
                    message.AddRange(mMovedFiles);
                }
                else if (0 < mDeletedFiles.Count)
                {
                    message.Add("Vss2Git: Delete/Destroy file(s)");

                    message.Add("");
                    message.Add("Deleted file(s):");
                    message.AddRange(mDeletedFiles);
                }
            }

            if (0 == message.Count)
            {
                message.Add(DefaultComment);
            }

            if (mIncludeVssMetaDataInComments && 0 < mRevisions.Count)
            {
                message.Add("");

                string indentStr = "";

                DateTime firstRevTime = mRevisions.First().DateTime;
                TimeSpan changeDuration = mChangeset.DateTime - firstRevTime;

                message.Add(string.Format("{0}Changeset {1} - {2} ({3} secs){4} {5} {6} file(s)",
                    indentStr, mChangeset.Id, mChangeset.DateTime.ToIsoTimestamp(), changeDuration.TotalSeconds,
                    "", mChangeset.User, mChangeset.Revisions.Count));

                foreach (VssItemRevision revision in mRevisions)
                {
                    message.Add(String.Format("{0}  {1} {2}@{3} {4}",
                        indentStr, revision.DateTime.ToIsoTimestamp(),
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
    };
}
