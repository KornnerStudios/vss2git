using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;
using SourceSafe.Analysis;
using SourceSafe.Analysis.PathMapping;
using SourceSafe.Jobs;
using SourceSafe.Logical;
using SourceSafe.Logical.Actions;
using SourceSafe.Logical.Items;

namespace SourceSafe.GitConversion
{
    /// <summary>
    /// Replays and commits changesets into a new repository.
    /// </summary>
    public sealed partial class GitExporter : QueuedWorkerBase, IGitStatistic
    {
        public static bool DryRunOutputTargetWorkDirPathsAsRelative { get; set; }
            = true;

        private readonly VssDatabase mDatabase;
        private readonly VssRevisionAnalyzer mRevisionAnalyzer;
        private readonly PseudoChangesetBuilder mChangesetBuilder;
        private readonly HashSet<string> mUsedTags = [];
        private IO.EmailDictionaryFileReader? mUserToEmailDictionary = null;
        private readonly HashSet<string> mExcludedProjects = [];
        private readonly HashSet<string> mExcludedFiles = [];
        private int mStatisticCommitCount = 0;
        private int mStatisticTagCount = 0;
        IO.FilePathMatcher? mProjectInclusionMatcher = null;
        IO.FilePathMatcher? mFileExclusionMatcher = null;

        public string EmailDomain { get; set; } = "localhost";
        public bool ResetRepo { get; set; } = true;
        public bool ForceAnnotatedTags { get; set; } = true;

        public bool IgnoreErrors { get; set; } = false;

        public int LoggerAutoFlushOnChangesetInterval { get; set; } = 64;
        public bool IncludeIgnoredFiles { get; set; }

        public bool DryRun { get; set; } = false;

        public string UserToEmailDictionaryFile
        {
            set => mUserToEmailDictionary = new(value);
        }

        public bool IncludeVssMetaDataInComments { get; set; } = false;
        public string? VssIncludedProjects { get; set; }
        public string? ExcludeFiles { get; set; }

        public string DefaultComment { get; set; } = "";

        internal static readonly char[] EmailPartsSeparator = ['@'];
        internal static readonly char[] EmailUserNamePartsSeparator = ['.'];
        private static readonly char[] PathListSeparator = [';'];

        public GitExporter(
            TrackedWorkQueue workQueue,
            IO.SimpleLogger logger,
            VssRevisionAnalyzer revisionAnalyzer,
            PseudoChangesetBuilder changesetBuilder)
            : base(workQueue, logger)
        {
            mDatabase = revisionAnalyzer.Database;
            mRevisionAnalyzer = revisionAnalyzer;
            mChangesetBuilder = changesetBuilder;
        }

        public void ExportToGit(
            IGitWrapper git)
        {
            if (!string.IsNullOrEmpty(VssIncludedProjects))
            {
                string[] includeProjectArray = VssIncludedProjects.Split(
                    PathListSeparator, StringSplitOptions.RemoveEmptyEntries);
                mProjectInclusionMatcher = new(includeProjectArray);
            }

            if (!string.IsNullOrEmpty(ExcludeFiles))
            {
                string[] excludeFileArray = ExcludeFiles.Split(
                    PathListSeparator, StringSplitOptions.RemoveEmptyEntries);
                mFileExclusionMatcher = new(excludeFileArray);
            }

            mWorkQueue.AddLast(workerCallback => ExportToGitWorkerCallback(workerCallback, git));
        }
        private void ExportToGitWorkerCallback(
            WorkerCallback workerCallback,
            IGitWrapper git)
        {
            var stopwatch = Stopwatch.StartNew();

            mLogger.WriteSectionSeparator();
            LogStatus(workerCallback, "Initializing repository");

            if (DryRun && DryRunOutputTargetWorkDirPathsAsRelative)
            {
                mLogger.WriteLine($"\t{VssPathMapper.RelativeWorkDirPrefix} = {git.GetRepoPath()}");
            }

            if (!string.IsNullOrEmpty(ExcludeFiles))
            {
                mLogger.WriteLine("\tExcluded projects/files: {0}", ExcludeFiles);
            }

            // create repository directory if it does not exist
            if (!DryRun && !Directory.Exists(git.GetRepoPath()))
            {
                Directory.CreateDirectory(git.GetRepoPath());
            }

            if (!DryRun)
            {
                while (!git.FindExecutable())
                {
                    // #TODO WinForms - this isn't CLI-ready
                    DialogResult button = MessageBox.Show("Git not found in PATH. " +
                        "If you need to modify your PATH variable, please " +
                        "restart the program for the changes to take effect.",
                        "Error", MessageBoxButtons.RetryCancel, MessageBoxIcon.Error);
                    if (button == DialogResult.Cancel)
                    {
                        mWorkQueue.Abort();
                        return;
                    }
                }

                if (!RetryCancel(delegate { git.Init(ResetRepo); }))
                {
                    return;
                }

                AbortRetryIgnore(delegate
                {
                    git.Configure();
                });
            }

            var pathMapper = new VssPathMapper();

            // create mappings for root projects
            foreach (VssProjectItem rootProject in mRevisionAnalyzer.RootProjects)
            {
                // root must be repo path here - the path mapper uses paths relative to this one
                string rootPath = git.GetRepoPath();
                pathMapper.SetProjectPath(rootProject.PhysicalName, rootPath, rootProject.LogicalPath);
            }

            if (DryRun && mChangesetBuilder.ChangesetsWithMeaningfulComments.Count > 0)
            {
                mLogger.WriteSectionSeparator();

                float meaningfulCommentPercentage = mChangesetBuilder.ChangesetsWithMeaningfulComments.Count / (float)mChangesetBuilder.Changesets.Count;
                meaningfulCommentPercentage *= 100.0f;

                mLogger.WriteLine($"Changesets with meaningful comments ({meaningfulCommentPercentage}%):");
                foreach (PseudoChangeset changeset in mChangesetBuilder.ChangesetsWithMeaningfulComments)
                {
                    mLogger.WriteLine($"\tChangeset {changeset.Id} {changeset.DateTime} {changeset.User}");
                    foreach (string commentLine in changeset.Comment)
                    {
                        mLogger.WriteLine($"\t\t{commentLine}");
                    }
                }
                mLogger.Flush();
            }

            // replay each changeset
            int changesetId = 1;
            List<PseudoChangeset> changesets = mChangesetBuilder.Changesets;
            mExcludedProjects.Clear();
            mExcludedFiles.Clear();
            mStatisticCommitCount = 0;
            mStatisticTagCount = 0;
            var replayStopwatch = new Stopwatch();
            var gitStopwatch = new Stopwatch();
            mUsedTags.Clear();
            foreach (PseudoChangeset changeset in changesets)
            {
                if (LoggerAutoFlushOnChangesetInterval > 0 && (changesetId % LoggerAutoFlushOnChangesetInterval) == 0)
                {
                    mLogger.Flush();
                }

                string changesetDesc = string.Format(CultureInfo.InvariantCulture,
                    "changeset {0} from {1}", changesetId, changeset.DateTime.ToIsoTimestamp());

                mLogger.WriteLine("//-------------------------------------------------------------------------//");

                // replay each revision in changeset
                LogStatus(workerCallback, "Replaying " + changesetDesc);

                var pendingCommits = new Dictionary<string, GitActions.CommitAction>();

                replayStopwatch.Start();
                try
                {
                    ReplayChangeset(pathMapper, changeset, ref pendingCommits);
                }
                finally
                {
                    replayStopwatch.Stop();
                }

                if (mWorkQueue.IsAborting)
                {
                    return;
                }

                if (!DryRun)
                {
                    // commit changes
                    LogStatus(workerCallback, "Committing " + changesetDesc);

                    gitStopwatch.Start();
                    try
                    {
                        IOrderedEnumerable<KeyValuePair<string, GitActions.CommitAction>> pendingCommitsOrderedByBranch = pendingCommits.OrderBy(x => x.Key);

                        foreach (KeyValuePair<string, GitActions.CommitAction> commit in pendingCommitsOrderedByBranch)
                        {
                            commit.Value.Run(mLogger, git, this);

                            if (mWorkQueue.IsAborting)
                            {
                                break;
                            }
                        }
                    }
                    finally
                    {
                        gitStopwatch.Stop();
                    }
                }

                if (mWorkQueue.IsAborting)
                {
                    return;
                }

                ++changesetId;
            }

            stopwatch.Stop();

            mLogger.WriteSectionSeparator();
            mLogger.WriteLine("Vss Projects : {0} excluded", mExcludedProjects.Count);
            mLogger.WriteLine("Vss Files: {0} excluded", mExcludedFiles.Count);
            mLogger.WriteLine("Git export complete in {0:HH:mm:ss}", new DateTime(stopwatch.ElapsedTicks));
            mLogger.WriteLine("Replay time: {0:HH:mm:ss}", new DateTime(replayStopwatch.ElapsedTicks));
            if (!DryRun)
            {
                mLogger.WriteLine("Git time: {0:HH:mm:ss}", new DateTime(gitStopwatch.ElapsedTicks));
            }
            mLogger.WriteLine("Git commits: {0}", mStatisticCommitCount);
            mLogger.WriteLine("Git tags: {0}", mStatisticTagCount);
        }

        private void ReplayChangeset(
            VssPathMapper pathMapper,
            PseudoChangeset changeset,
            ref Dictionary<string, GitActions.CommitAction> pendingCommits)
        {
            foreach (VssItemRevision revision in changeset.Revisions)
            {
                ReplayRevision(pathMapper, changeset, revision, ref pendingCommits);
            }
        }

        private void ReplayRevision(
            VssPathMapper pathMapper,
            PseudoChangeset changeset,
            VssItemRevision revision,
            ref Dictionary<string, GitActions.CommitAction> pendingCommits)
        {
            string indentStr = IO.OutputUtil.GetIndentString(1);

            VssActionType actionType = revision.Action.Type;
            if (revision.Item.IsProject)
            {
                // note that project path (and therefore target path) can be
                // null if a project was moved and its original location was
                // subsequently destroyed
                VssItemName project = revision.Item;
#if DEBUG
                string projectName = project.LogicalName;
#endif // DEBUG
                List<string>? projectLogicalPath = pathMapper.GetProjectLogicalPath(project.PhysicalName);
                List<string>? projectWorkDirPath = pathMapper.GetProjectWorkDirPath(project.PhysicalName);

                VssItemName? target = null;
                List<string>? targetWorkDirPath = null;
                List<string>? targetLogicalPath = null;

                if (projectLogicalPath == null && projectWorkDirPath == null && actionType == VssActionType.Create)
                {
                    if (revision.Action is VssNamedActionBase namedAction)
                    {
                        target = namedAction.Name;
                    }

                    if (target != null)
                    {
                        mLogger.Write(indentStr);
                        mLogger.WriteLine("{0}: {1} {2}", revision.Item.ToString(), actionType, target.LogicalName);
                        pathMapper.CreateItem(project);
                        projectLogicalPath = pathMapper.GetProjectLogicalPath(project.PhysicalName);
                        projectWorkDirPath = pathMapper.GetProjectWorkDirPath(project.PhysicalName);
                    }
                }
                else
                {
                    if (revision.Action is VssNamedActionBase namedAction)
                    {
                        target = namedAction.Name;
                        if (projectLogicalPath != null)
                        {
                            targetLogicalPath = new List<string>(projectLogicalPath)
                            {
                                target.LogicalName
                            };
                        }

                        if (projectWorkDirPath != null)
                        {
                            targetWorkDirPath = new List<string>(projectWorkDirPath)
                            {
                                target.LogicalName
                            };
                        }
                    }
                }

                bool isAddAction = false;
                bool writeProject = false;
                bool writeFile = false;
                VssItemPathMappingBase? itemInfo = null;
                switch (actionType)
                {
                    case VssActionType.Label:
                    {
                        if (projectLogicalPath == null)
                        {
                            throw new InvalidOperationException($"{actionType}: Project logical path is null");
                        }

                        string labelName = ((VssLabelAction)revision.Action).Label;

                        if (string.IsNullOrEmpty(labelName))
                        {
                            mLogger.Write(indentStr);
                            mLogger.WriteLine("NOTE: Ignoring empty label");
                        }
                        else if (ShouldLogicalPathBeIncluded(pathMapper, true, projectLogicalPath))
                        {
                            GitActions.CommitAction pendingCommit = GetOrCreatePendingCommitForProject(ref pendingCommits, changeset, pathMapper, project);

                            // defer tagging until after commit
                            pendingCommit.AddTag(revision, CreateGitTagAction(labelName, revision.User, revision.Comment, revision.DateTime));
                        }

                        break;
                    }

                    case VssActionType.Create:
                    {
                        // ignored; items are actually created when added to a project
                        break;
                    }

                    case VssActionType.Add:
                    {
                        if (projectLogicalPath == null)
                        {
                            throw new InvalidOperationException($"{actionType}: Project logical path is null");
                        }
                        if (target == null)
                        {
                            throw new InvalidOperationException($"{actionType}: Target item is null");
                        }

                        GitActions.CommitAction pendingCommit = GetOrCreatePendingCommitForProject(ref pendingCommits, changeset, pathMapper, project);
                        SourceSafeConstants.MarkUnusedVariable(ref pendingCommit);

                        string projectDesc = GetProjectDescription(pathMapper, revision, project, projectLogicalPath);

                        mLogger.Write(indentStr);
                        mLogger.WriteLine("{0}: {1} {2}", projectDesc, actionType, target.LogicalName);

                        itemInfo = pathMapper.AddItem(project, target, mRevisionAnalyzer.IsDestroyed(project.PhysicalName, target.PhysicalName));

                        isAddAction = ShouldLogicalPathBeIncluded(pathMapper, target.IsProject, targetLogicalPath ?? projectLogicalPath);

                        break;
                    }

                    case VssActionType.Share:
                    {
                        if (projectLogicalPath == null)
                        {
                            throw new InvalidOperationException($"{actionType}: Project logical path is null");
                        }
                        if (target == null)
                        {
                            throw new InvalidOperationException($"{actionType}: Target item is null");
                        }

                        var shareAction = (VssShareAction)revision.Action;

                        GitActions.CommitAction pendingCommit = GetOrCreatePendingCommitForProject(ref pendingCommits, changeset, pathMapper, project);
                        SourceSafeConstants.MarkUnusedVariable(ref pendingCommit);

                        string projectDesc = GetProjectDescription(pathMapper, revision, project, projectLogicalPath);

                        mLogger.Write(indentStr);
                        if (shareAction.Pinned)
                        {
                            mLogger.WriteLine($"{projectDesc}: {actionType} {target.LogicalName}, Pin {shareAction.Revision}");
                            itemInfo = pathMapper.AddItem(project, target, mRevisionAnalyzer.IsDestroyed(project.PhysicalName, target.PhysicalName), shareAction.Revision);
                        }
                        else
                        {
                            mLogger.WriteLine($"{projectDesc}: {actionType} {target.LogicalName}");
                            itemInfo = pathMapper.AddItem(project, target, mRevisionAnalyzer.IsDestroyed(project.PhysicalName, target.PhysicalName));
                        }

                        isAddAction = ShouldLogicalPathBeIncluded(pathMapper, target.IsProject, targetLogicalPath);

                        break;
                    }

                    case VssActionType.Recover:
                    {
                        if (projectLogicalPath == null)
                        {
                            throw new InvalidOperationException($"{actionType}: Project logical path is null");
                        }
                        if (target == null)
                        {
                            throw new InvalidOperationException($"{actionType}: Target item is null");
                        }

                        GitActions.CommitAction pendingCommit = GetOrCreatePendingCommitForProject(ref pendingCommits, changeset, pathMapper, project);
                        SourceSafeConstants.MarkUnusedVariable(ref pendingCommit);

                        string projectDesc = GetProjectDescription(pathMapper, revision, project, projectLogicalPath);

                        mLogger.Write(indentStr);
                        mLogger.WriteLine($"{projectDesc}: {actionType} {target.LogicalName}");

                        itemInfo = pathMapper.RecoverItem(project, target, mRevisionAnalyzer.IsDestroyed(project.PhysicalName, target.PhysicalName));

                        isAddAction = ShouldLogicalPathBeIncluded(pathMapper, target.IsProject, targetLogicalPath!);

                        break;
                    }

                    case VssActionType.Delete:
                    case VssActionType.Destroy:
                    {
                        if (projectLogicalPath == null)
                        {
                            throw new InvalidOperationException($"{actionType}: Project logical path is null");
                        }
                        if (target == null)
                        {
                            throw new InvalidOperationException($"{actionType}: Target item is null");
                        }

                        GitActions.CommitAction pendingCommit = GetOrCreatePendingCommitForProject(ref pendingCommits, changeset, pathMapper, project);

                        string projectDesc = GetProjectDescription(pathMapper, revision, project, projectLogicalPath);

                        mLogger.Write(indentStr);
                        mLogger.WriteLine($"{projectDesc}: {actionType} {target.LogicalName}");

                        itemInfo = pathMapper.DeleteItem(project, target, (VssActionType.Destroy == actionType));

                        if (targetLogicalPath != null && targetWorkDirPath != null)
                        {
                            if (ShouldLogicalPathBeIncluded(pathMapper, target.IsProject, targetLogicalPath))
                            {
                                string targetWorkDirPathAsString = VssPathMapper.WorkDirPathToString(targetWorkDirPath);

                                if (target.IsProject)
                                {
                                    var projectInfo = (VssProjectPathMapping)itemInfo;

                                    if (VssActionType.Destroy == actionType || !projectInfo.Destroyed)
                                    {
                                        bool containsFiles = projectInfo.ContainsFiles();

                                        pendingCommit.AddAction(revision, new GitActions.DeleteDirectoryAction(targetWorkDirPathAsString, containsFiles), containsFiles);
                                    }
                                    else
                                    {
                                        mLogger.Write(indentStr);
                                        mLogger.WriteLine("NOTE: Skipping move to because {0} in project {1} is destroyed",
                                            target.LogicalName, VssPathMapper.LogicalPathToString(projectLogicalPath!));
                                    }
                                }
                                else
                                {
                                    bool destroyed = pathMapper.GetFileDestroyed(project, target.PhysicalName);

                                    if (VssActionType.Destroy == actionType || !destroyed)
                                    {
                                        // not sure how it can happen, but a project can evidently
                                        // contain another file with the same logical name, so check
                                        // that this is not the case before deleting the file
                                        if (VssActionType.Destroy != actionType && pathMapper.ProjectContainsLogicalName(project, target))
                                        {
                                            mLogger.Write(indentStr);
                                            mLogger.WriteLine("NOTE: {0} contains another file named {1}; not deleting file",
                                                projectDesc, target.LogicalName);
                                        }
                                        else
                                        {
                                            string targetLogicalPathAsString = VssPathMapper.LogicalPathToString(targetLogicalPath);

                                            pendingCommit.DeleteFileOrDirectory(revision, new GitActions.DeleteFileAction(targetWorkDirPathAsString), targetLogicalPathAsString);
                                        }
                                    }
                                    else
                                    {
                                        mLogger.Write(indentStr);
                                        mLogger.WriteLine("NOTE: Skipping move to because {0} in project {1} is destroyed",
                                            target.LogicalName, VssPathMapper.LogicalPathToString(projectLogicalPath));
                                    }
                                }
                            }
                        }

                        break;
                    }

                    case VssActionType.Rename:
                    {
                        if (projectLogicalPath == null)
                        {
                            throw new InvalidOperationException($"{actionType}: Project logical path is null");
                        }
                        if (projectWorkDirPath == null)
                        {
                            throw new InvalidOperationException($"{actionType}: Project work directory path is null");
                        }
                        if (target == null)
                        {
                            throw new InvalidOperationException($"{actionType}: Target item is null");
                        }

                        var renameAction = (VssRenameAction)revision.Action;

                        GitActions.CommitAction pendingCommit = GetOrCreatePendingCommitForProject(ref pendingCommits, changeset, pathMapper, project);

                        string projectDesc = GetProjectDescription(pathMapper, revision, project, projectLogicalPath);

                        mLogger.Write(indentStr);
                        mLogger.WriteLine($"{projectDesc}: {actionType} {renameAction.OriginalName} to {target.LogicalName}");

                        itemInfo = pathMapper.RenameItem(target);

                        if (targetLogicalPath != null && targetWorkDirPath != null)
                        {
                            if (ShouldLogicalPathBeIncluded(pathMapper, target.IsProject, targetLogicalPath))
                            {
                                var sourceLogicalPath = new List<string>(projectLogicalPath)
                                {
                                    renameAction.OriginalName
                                };

                                var sourceWorkDirPath = new List<string>(projectWorkDirPath)
                                {
                                    renameAction.OriginalName
                                };

                                string sourceWorkDirPathAsString = VssPathMapper.WorkDirPathToString(sourceWorkDirPath);
                                string targetWorkDirPathAsString = VssPathMapper.WorkDirPathToString(targetWorkDirPath);

                                bool destroyed = target.IsProject
                                    ? ((VssProjectPathMapping)itemInfo).Destroyed
                                    : pathMapper.GetFileDestroyed(project, target.PhysicalName);

                                if (!destroyed)
                                {
                                    // renaming a file or a project that contains files?
                                    if (itemInfo is not VssProjectPathMapping projectInfo || projectInfo.ContainsFiles())
                                    {
                                        if (!DryRun)
                                        {
                                            string sourceLogicalPathAsString = VssPathMapper.LogicalPathToString(sourceLogicalPath);
                                            string targetLogicalPathAsString = VssPathMapper.LogicalPathToString(targetLogicalPath);

                                            string message = $"{targetLogicalPathAsString} (from {sourceLogicalPathAsString})";

                                            if (target.IsProject)
                                            {
                                                pendingCommit.MoveFileOrDirectory(
                                                    revision,
                                                    new GitActions.MoveDirectoryAction(sourceWorkDirPathAsString, targetWorkDirPathAsString, true),
                                                    message);
                                            }
                                            else
                                            {
                                                pendingCommit.MoveFileOrDirectory(
                                                    revision,
                                                    new GitActions.MoveFileAction(sourceWorkDirPathAsString, targetWorkDirPathAsString),
                                                    message);
                                            }
                                        }
                                    }
                                    else
                                    {
                                        // git doesn't care about directories with no files
                                        pendingCommit.AddAction(revision, new GitActions.MoveDirectoryAction(sourceWorkDirPathAsString, targetWorkDirPathAsString, false), false);
                                    }
                                }
                                else
                                {
                                    mLogger.Write(indentStr);
                                    mLogger.WriteLine("NOTE: Skipping move to because {0} in project {1} is destroyed",
                                        target.LogicalName, VssPathMapper.LogicalPathToString(projectLogicalPath));
                                }
                            }
                        }

                        break;
                    }

                    case VssActionType.MoveFrom:
                    {
                        if (projectLogicalPath == null)
                        {
                            throw new InvalidOperationException($"{actionType}: Project logical path is null");
                        }
                        if (target == null)
                        {
                            throw new InvalidOperationException($"{actionType}: Target item is null");
                        }

                        // if both MoveFrom & MoveTo are present (e.g.
                        // one of them has not been destroyed), only one
                        // can succeed, so check that the source exists

                        var moveFromAction = (VssMoveFromAction)revision.Action;

                        GitActions.CommitAction pendingCommit = GetOrCreatePendingCommitForProject(ref pendingCommits, changeset, pathMapper, project);

                        string projectDesc = GetProjectDescription(pathMapper, revision, project, projectLogicalPath);

                        string targetLogicalPathAsString = VssPathMapper.LogicalPathToString(targetLogicalPath);

                        mLogger.Write(indentStr);
                        mLogger.WriteLine($"{projectDesc}: Move from {moveFromAction.OriginalProject} to {targetLogicalPathAsString}");

                        if (pathMapper.IsProjectRooted(moveFromAction.Name.PhysicalName))
                        {
                            List<string>? sourceLogicalPath = pathMapper.GetProjectLogicalPath(moveFromAction.Name.PhysicalName);
                            List<string>? sourceWorkDirPath = pathMapper.GetProjectWorkDirPath(moveFromAction.Name.PhysicalName);

                            VssProjectPathMapping projectInfo = pathMapper.MoveProjectFrom(project, target, moveFromAction.OriginalProject);

                            if (targetLogicalPath != null && targetWorkDirPath != null && !projectInfo.Destroyed)
                            {
                                if (ShouldLogicalPathBeIncluded(pathMapper, target.IsProject, targetLogicalPath))
                                {
                                    string targetWorkDirPathAsString = VssPathMapper.WorkDirPathToString(targetWorkDirPath);

                                    if (sourceLogicalPath != null && sourceWorkDirPath != null)
                                    {
                                        string sourceWorkDirPathAsString = VssPathMapper.WorkDirPathToString(sourceWorkDirPath);

                                        if (Directory.Exists(sourceWorkDirPathAsString))
                                        {
                                            if (projectInfo.ContainsFiles())
                                            {
                                                string sourceLogicalPathAsString = VssPathMapper.LogicalPathToString(sourceLogicalPath);

                                                string message = $"{targetLogicalPathAsString} (from {sourceLogicalPathAsString})";

                                                pendingCommit.MoveFileOrDirectory(revision, new GitActions.MoveDirectoryAction(sourceWorkDirPathAsString, targetWorkDirPathAsString, true), message);
                                            }
                                            else
                                            {
                                                pendingCommit.AddAction(revision, new GitActions.MoveDirectoryAction(sourceWorkDirPathAsString, targetWorkDirPathAsString, false), false);
                                            }
                                        }
                                        else
                                        {
                                            // project was moved from a now-destroyed project
                                            writeProject = true;
                                        }
                                    }
                                    else
                                    {
                                        // project was moved from a now-destroyed project
                                        writeProject = true;
                                    }
                                }
                            }
                            else if (projectLogicalPath != null && projectInfo.Destroyed)
                            {
                                mLogger.Write(indentStr);
                                mLogger.WriteLine("NOTE: Skipping move to because {0} is destroyed",
                                    VssPathMapper.LogicalPathToString(projectLogicalPath));
                            }
                        }
                        else
                        {
                            // the moved project is currently not part of project tree

                            bool destroyed = mRevisionAnalyzer.IsDestroyed(project.PhysicalName, target.PhysicalName);

                            if (!destroyed)
                            {
                                mLogger.Write(indentStr);
                                mLogger.WriteLine("NOTE: Moving unmapped project {0} from {1} to {2}",
                                    target.LogicalName, moveFromAction.OriginalProject, VssPathMapper.LogicalPathToString(projectLogicalPath));

                                // add it again
                                itemInfo = pathMapper.AddItem(project, target, destroyed);

                                isAddAction = ShouldLogicalPathBeIncluded(pathMapper, target.IsProject, targetLogicalPath);
                            }
                            else
                            {
                                mLogger.Write(indentStr);
                                mLogger.WriteLine("NOTE: Skipping move to because {0} in project {1} is destroyed",
                                    target.LogicalName, VssPathMapper.LogicalPathToString(projectLogicalPath));
                            }
                        }

                        break;
                    }

                    case VssActionType.MoveTo:
                    {
                        if (projectLogicalPath == null)
                        {
                            throw new InvalidOperationException($"{actionType}: Project logical path is null");
                        }
                        if (target == null)
                        {
                            throw new InvalidOperationException($"{actionType}: Target item is null");
                        }

                        // handle actual moves in MoveFrom; this just does cleanup of destroyed projects
                        var moveToAction = (VssMoveToAction)revision.Action;

                        GitActions.CommitAction pendingCommit = GetOrCreatePendingCommitForProject(ref pendingCommits, changeset, pathMapper, project);

                        string projectDesc = GetProjectDescription(pathMapper, revision, project, projectLogicalPath);

                        string targetLogicalPathAsString = VssPathMapper.LogicalPathToString(targetLogicalPath);

                        mLogger.WriteLine($"{projectDesc}: Move to {moveToAction.NewProject} from {targetLogicalPathAsString}");

                        VssProjectPathMapping projectInfo = pathMapper.MoveProjectTo(project, target, moveToAction.NewProject);

                        if (!projectInfo.Destroyed && targetLogicalPath != null && targetWorkDirPath != null)
                        {
                            if (ShouldLogicalPathBeIncluded(pathMapper, target.IsProject, targetLogicalPath))
                            {
                                string targetWorkDirPathAsString = VssPathMapper.WorkDirPathToString(targetWorkDirPath);

                                // project was moved to a now-destroyed project; remove empty directory
                                pendingCommit.AddAction(revision, new GitActions.DeleteDirectoryAction(targetWorkDirPathAsString, false), false);
                            }
                        }

                        break;
                    }

                    case VssActionType.Pin:
                    {
                        if (projectLogicalPath == null)
                        {
                            throw new InvalidOperationException($"{actionType}: Project logical path is null");
                        }
                        if (target == null)
                        {
                            throw new InvalidOperationException($"{actionType}: Target item is null");
                        }

                        GitActions.CommitAction pendingCommit = GetOrCreatePendingCommitForProject(ref pendingCommits, changeset, pathMapper, project);
                        SourceSafeConstants.MarkUnusedVariable(ref pendingCommit);

                        string projectDesc = GetProjectDescription(pathMapper, revision, project, projectLogicalPath);

                        bool destroyed = mRevisionAnalyzer.IsDestroyed(project.PhysicalName, target.PhysicalName);
                        var pinAction = (VssPinAction)revision.Action;

                        mLogger.Write(indentStr);
                        if (pinAction.Pinned)
                        {
                            mLogger.WriteLine($"{projectDesc}: Pin {target.LogicalName}");
                            itemInfo = pathMapper.PinItem(project, target, pinAction.Revision, destroyed);
                            writeFile = !destroyed && ShouldLogicalPathBeIncluded(pathMapper, target.IsProject, targetLogicalPath);
                        }
                        else
                        {
                            mLogger.WriteLine($"{projectDesc}: Unpin {target.LogicalName}");
                            itemInfo = pathMapper.UnpinItem(project, target, destroyed);
                            writeFile = !destroyed && ShouldLogicalPathBeIncluded(pathMapper, target.IsProject, targetLogicalPath);
                        }

                        break;
                    }

                    case VssActionType.Branch:
                    {
                        if (projectLogicalPath == null)
                        {
                            throw new InvalidOperationException($"{actionType}: Project logical path is null");
                        }
                        if (target == null)
                        {
                            throw new InvalidOperationException($"{actionType}: Target item is null");
                        }

                        GitActions.CommitAction pendingCommit = GetOrCreatePendingCommitForProject(ref pendingCommits, changeset, pathMapper, project);
                        SourceSafeConstants.MarkUnusedVariable(ref pendingCommit);

                        string projectDesc = GetProjectDescription(pathMapper, revision, project, projectLogicalPath);

                        var branchAction = (VssBranchAction)revision.Action;
                        mLogger.Write(indentStr);
                        mLogger.WriteLine($"{projectDesc}: {actionType} {target.LogicalName}");

                        itemInfo = pathMapper.BranchFile(project, target, branchAction.Source, mRevisionAnalyzer.IsDestroyed(project.PhysicalName, target.PhysicalName));

                        break;
                    }

                    // currently ignored
                    case VssActionType.Archive:
                    {
                        if (projectLogicalPath == null)
                        {
                            throw new InvalidOperationException($"{actionType}: Project logical path is null");
                        }
                        if (target == null)
                        {
                            throw new InvalidOperationException($"{actionType}: Target item is null");
                        }

                        GitActions.CommitAction pendingCommit = GetOrCreatePendingCommitForProject(ref pendingCommits, changeset, pathMapper, project);
                        SourceSafeConstants.MarkUnusedVariable(ref pendingCommit);

                        string projectDesc = GetProjectDescription(pathMapper, revision, project, projectLogicalPath);

                        var archiveAction = (VssArchiveAction)revision.Action;
                        mLogger.Write(indentStr);
                        mLogger.WriteLine($"{projectDesc}: Archive {target.LogicalName} to {archiveAction.ArchivePath} (ignored)");

                        break;
                    }

                    case VssActionType.Restore:
                    {
                        if (projectLogicalPath == null)
                        {
                            throw new InvalidOperationException($"{actionType}: Project logical path is null");
                        }
                        if (target == null)
                        {
                            throw new InvalidOperationException($"{actionType}: Target item is null");
                        }

                        GitActions.CommitAction pendingCommit = GetOrCreatePendingCommitForProject(ref pendingCommits, changeset, pathMapper, project);
                        SourceSafeConstants.MarkUnusedVariable(ref pendingCommit);

                        string projectDesc = GetProjectDescription(pathMapper, revision, project, projectLogicalPath);

                        var restoreAction = (VssRestoreAction)revision.Action;
                        mLogger.Write(indentStr);
                        mLogger.WriteLine($"{projectDesc}: Restore {target.LogicalName} from archive {restoreAction.ArchivePath}");

                        itemInfo = pathMapper.AddItem(project, target, mRevisionAnalyzer.IsDestroyed(project.PhysicalName, target.PhysicalName));
                        isAddAction = ShouldLogicalPathBeIncluded(pathMapper, target.IsProject, targetLogicalPath);

                        break;
                    }
                }

                if (targetLogicalPath != null && targetWorkDirPath != null)
                {
                    if (target == null)
                    {
                        throw new InvalidOperationException($"Target item is null");
                    }

                    if (isAddAction)
                    {
                        if (target.IsProject)
                        {
                            if (ShouldLogicalPathBeIncluded(pathMapper, true, targetLogicalPath))
                            {
                                GitActions.CommitAction pendingCommit = GetOrCreatePendingCommitForProject(ref pendingCommits, changeset, pathMapper, project);

                                var projectInfo = (VssProjectPathMapping)itemInfo!;

                                if (!projectInfo.Destroyed)
                                {
                                    string targetWorkDirPathAsString = VssPathMapper.WorkDirPathToString(targetWorkDirPath);
                                    pendingCommit.AddAction(revision, new GitActions.CreateDirectoryAction(targetWorkDirPathAsString), false);
                                    writeProject = true;
                                }
                                else
                                {
                                    string targetLogicalPathAsString = VssPathMapper.WorkDirPathToString(targetLogicalPath);
                                    mLogger.Write(indentStr);
                                    mLogger.WriteLine($"NOTE: Skipping destroyed project: {targetLogicalPathAsString}");
                                }
                            }
                        }
                        else
                        {
                            writeFile = true;
                        }
                    }

                    if (writeProject && pathMapper.IsProjectRooted(target.PhysicalName))
                    {
                        // create all contained subdirectories
                        foreach (VssProjectPathMapping projectInfo in pathMapper.GetAllProjects(target.PhysicalName))
                        {
                            if (!ShouldLogicalPathBeIncluded(pathMapper, true, projectInfo.GetLogicalPath()))
                            {
                                continue;
                            }

                            GitActions.CommitAction pendingCommit = GetOrCreatePendingCommitForProject(ref pendingCommits, changeset, pathMapper, project);

                            string projectDesc = GetProjectDescription(pathMapper, revision, project, projectLogicalPath);

                            if (DryRun)
                            {
                                mLogger.Write(indentStr);
                                mLogger.WriteLine($"{projectDesc}: Creating subdirectory {projectInfo.LogicalName}");
                            }

                            List<string> targetProjectWorkDirPath = projectInfo.GetWorkDirPath()!;
                            string targetWorkDirPathAsString = VssPathMapper.WorkDirPathToString(targetProjectWorkDirPath);

                            pendingCommit.AddAction(revision, new GitActions.CreateDirectoryAction(targetWorkDirPathAsString), false);
                        }

                        // write current rev of all contained files
                        foreach (VssFilePathMapping fileInfo in pathMapper.GetAllFiles(target.PhysicalName))
                        {
                            IEnumerable<Tuple<List<string>, List<string>>> paths = pathMapper.GetFilePaths(fileInfo.PhysicalName, target.PhysicalName, revision.Version, mLogger);

                            foreach (Tuple<List<string>, List<string>> p in paths)
                            {
                                if (!ShouldLogicalPathBeIncluded(pathMapper, false, p.Item1))
                                {
                                    continue;
                                }

                                GitActions.CommitAction pendingCommit = GetOrCreatePendingCommitForLogicalPath(ref pendingCommits, changeset, p.Item1);

                                string targetWorkDirPathAsString = VssPathMapper.WorkDirPathToString(p.Item2);

                                if (DryRun)
                                {
                                    string targetWorkDirPathAsStringForDryRun = DryRunOutputTargetWorkDirPathsAsRelative
                                        ? VssPathMapper.RelativeWorkDirPathToString(p.Item2)
                                        : targetWorkDirPathAsString;

                                    mLogger.Write(indentStr);
                                    mLogger.WriteLine($"{targetWorkDirPathAsStringForDryRun}: {actionType} revision {fileInfo.Version}");
                                }

                                pendingCommit.AddFile(revision, new GitActions.WriteFileAction(mDatabase, fileInfo.PhysicalName, fileInfo.Version, targetWorkDirPathAsString), VssPathMapper.LogicalPathToString(p.Item1));
                            }
                        }
                    }
                    else if (writeFile)
                    {
                        if (ShouldLogicalPathBeIncluded(pathMapper, false, targetLogicalPath))
                        {
                            bool destroyed = pathMapper.GetFileDestroyed(project, target.PhysicalName);

                            GitActions.CommitAction pendingCommit = GetOrCreatePendingCommitForLogicalPath(ref pendingCommits, changeset, targetLogicalPath);

                            string targetLogicalPathAsString = VssPathMapper.LogicalPathToString(targetLogicalPath);

                            if (!destroyed)
                            {
                                // write current rev to working path
                                int version = pathMapper.GetFileVersion(project, target.PhysicalName);

                                string targetWorkDirPathAsString = VssPathMapper.WorkDirPathToString(targetWorkDirPath);

                                if (DryRun)
                                {
                                    string targetWorkDirPathAsStringForDryRun = DryRunOutputTargetWorkDirPathsAsRelative
                                        ? VssPathMapper.RelativeWorkDirPathToString(targetWorkDirPath)
                                        : targetWorkDirPathAsString;

                                    mLogger.Write(indentStr);
                                    mLogger.WriteLine($"{targetWorkDirPathAsStringForDryRun}: {actionType} revision {version}");
                                }

                                pendingCommit.AddFile(revision, new GitActions.WriteFileAction(mDatabase, target.PhysicalName, version, targetWorkDirPathAsString), targetLogicalPathAsString);
                            }
                            else
                            {
                                mLogger.Write(indentStr);
                                mLogger.WriteLine($"NOTE: Skipping destroyed file: {targetLogicalPathAsString}");
                            }
                        }
                    }
                }
            }
            // item is a file, not a project
            else if (actionType == VssActionType.Edit || actionType == VssActionType.Branch)
            {
                // if the action is Branch, the following code is necessary only if the item
                // was branched from a file that is not part of the migration subset; it will
                // make sure we start with the correct revision instead of the first revision

                VssItemName target = revision.Item;

                // update current rev
                pathMapper.SetFileVersion(target, revision.Version);

                // write current rev to all sharing projects (thus null)
                IEnumerable<Tuple<List<string>, List<string>>> paths = pathMapper.GetFilePaths(target.PhysicalName, null, revision.Version, mLogger);

                foreach (Tuple<List<string>, List<string>> p in paths)
                {
                    if (!ShouldLogicalPathBeIncluded(pathMapper, false, p.Item1))
                    {
                        continue;
                    }

                    GitActions.CommitAction pendingCommit = GetOrCreatePendingCommitForLogicalPath(ref pendingCommits, changeset, p.Item1);

                    string targetWorkDirPathAsString = VssPathMapper.WorkDirPathToString(p.Item2);

                    if (DryRun)
                    {
                        string targetWorkDirPathAsStringForDryRun = DryRunOutputTargetWorkDirPathsAsRelative
                            ? VssPathMapper.RelativeWorkDirPathToString(p.Item2)
                            : targetWorkDirPathAsString;

                        mLogger.Write(indentStr);
                        mLogger.WriteLine($"{targetWorkDirPathAsStringForDryRun}: {actionType} revision {revision.Version}");
                    }

                    pendingCommit.WriteFile(revision, new GitActions.WriteFileAction(mDatabase, target.PhysicalName, revision.Version, targetWorkDirPathAsString));
                }
            }
        }

        private bool ShouldLogicalPathBeIncluded(
            VssPathMapper _/*pathMapper*/,
            bool isProject,
            List<string>? logicalPath)
        {
            bool shouldBeIncluded = false;

            if (logicalPath != null)
            {
                string path = VssPathMapper.LogicalPathToString(logicalPath);

                if (mProjectInclusionMatcher != null)
                {
                    if (mProjectInclusionMatcher.Matches(path))
                    {
                        if (mFileExclusionMatcher == null || !mFileExclusionMatcher.Matches(path))
                        {
                            shouldBeIncluded = true;
                        }
                    }
                }
                else
                {
                    shouldBeIncluded = true;
                }

                if (!shouldBeIncluded)
                {
                    if (isProject)
                    {
                        if (DryRun)
                        {
                            mLogger.Write(IO.OutputUtil.GetIndentString(1));
                            mLogger.WriteLine($"Excluding project {path}");
                        }
                        mExcludedProjects.Add(path);
                    }
                    else
                    {
                        if (DryRun)
                        {
                            mLogger.Write(IO.OutputUtil.GetIndentString(1));
                            mLogger.WriteLine($"Excluding file {path}");
                        }
                        mExcludedFiles.Add(path);
                    }
                }
            }
            else
            {
                shouldBeIncluded = false;
            }

            return shouldBeIncluded;
        }

        private string GetProjectDescription(
            VssPathMapper _/*pathMapper*/,
            VssItemRevision revision,
            VssItemName project,
            List<string>? projectPath)
        {
            string projectDesc;

            if (projectPath != null)
            {
                projectDesc = VssPathMapper.LogicalPathToString(projectPath);
            }
            else
            {
                projectDesc = revision.Item.ToString();

                mLogger.Write(IO.OutputUtil.GetIndentString(1));
                mLogger.WriteLine($"NOTE: {project.LogicalName} is currently unmapped");
            }

            return projectDesc;
        }

        private GitActions.CommitAction GetOrCreatePendingCommitForLogicalPath(
            ref Dictionary<string, GitActions.CommitAction> pendingCommits,
            PseudoChangeset changeset,
            List<string>? _/*logicalPath*/)
        {
            // #REVIEW this needs to be more robust, but we cannot call IGitWrapper.GetDefaultBranch() from here
            const string pendingBranch = AbstractGitWrapper.DefaultCheckoutBranch;

            if (!pendingCommits.TryGetValue(pendingBranch, out GitActions.CommitAction? pendingCommit))
            {
                pendingCommit = CreateGitCommitAction(changeset);
                pendingCommits[pendingBranch] = pendingCommit;
            }

            return pendingCommit;
        }

        private GitActions.CommitAction GetOrCreatePendingCommitForProject(
            ref Dictionary<string, GitActions.CommitAction> pendingCommits,
            PseudoChangeset changeset,
            VssPathMapper pathMapper,
            VssItemName project)
        {
            return GetOrCreatePendingCommitForLogicalPath(ref pendingCommits, changeset, pathMapper.GetProjectLogicalPath(project.PhysicalName));
        }

        [Obsolete("Unused")]
        private static string GetPendingBranchFromLogicalProjectPath(
            List<string>? logicalProjectPath)
        {
            string pendingBranch = "";

            if (logicalProjectPath != null && 2 <= logicalProjectPath.Count && SourceSafeConstants.RootProjectName == logicalProjectPath[0])
            {
                pendingBranch = logicalProjectPath[1];
            }

            return pendingBranch;
        }

        private GitActions.CommitAction CreateGitCommitAction(
            PseudoChangeset changeset)
        {
            return new GitActions.CommitAction(
                changeset,
                GetUser(changeset.User),
                GetEmail(changeset.User),
                changeset.DateTime.ConvertAmbiguousTimeToUtc(mLogger),
                IncludeVssMetaDataInComments);
        }

        private GitActions.CreateTagAction CreateGitTagAction(
            string labelName,
            string user,
            string comment,
            DateTime localTime)
        {
            string tagName = GetTagFromLabel(labelName);

            // annotated tags require (and are implied by) a tag message;
            // tools like Mercurial's git converter only import annotated tags
            string tagMessage = comment;
            if (String.IsNullOrEmpty(tagMessage) && ForceAnnotatedTags)
            {
                // use the original VSS label as the tag message if none was provided
                tagMessage = labelName;
            }

            return new GitActions.CreateTagAction(tagName, GetUser(user), GetEmail(user), tagMessage, localTime.ConvertAmbiguousTimeToUtc(mLogger));
        }

        private bool RetryCancel(
            ThreadStart work)
        {
            // #TODO WinForms - this isn't CLI-ready
            return AbortRetryIgnore(work, MessageBoxButtons.RetryCancel);
        }

        private bool AbortRetryIgnore(
            ThreadStart work)
        {
            // #TODO WinForms - this isn't CLI-ready
            return AbortRetryIgnore(work, MessageBoxButtons.AbortRetryIgnore);
        }

        // #TODO WinForms - this isn't CLI-ready
        private bool AbortRetryIgnore(
            ThreadStart work,
            MessageBoxButtons buttons)
        {
            bool retry;
            do
            {
                try
                {
                    work();
                    return true;
                }
                catch (Exception e)
                {
                    string message = LogException(e);

                    message += "\nSee log file for more information.";

                    if (IgnoreErrors)
                    {
                        retry = false;
                        continue;
                    }

                    // #TODO WinForms - this isn't CLI-ready
                    DialogResult button = MessageBox.Show(message, "Error", buttons, MessageBoxIcon.Error);
                    switch (button)
                    {
                        case DialogResult.Retry:
                            retry = true;
                            break;
                        case DialogResult.Ignore:
                            retry = false;
                            break;
                        default:
                            retry = false;
                            mWorkQueue.Abort();
                            break;
                    }
                }
            } while (retry);
            return false;
        }

        private static string FirstCharToUpper(
            string input)
        {
            ArgumentException.ThrowIfNullOrEmpty(input, nameof(input));
            return string.Concat(input.First().ToString().ToUpper(), input.AsSpan(1));
        }

        private string GetUser(
            string user)
        {
            if (mUserToEmailDictionary != null)
            {
                string? email = mUserToEmailDictionary.GetValue(user, null);

                if (email != null)
                {
                    string[] emailParts = email.Split(EmailPartsSeparator, 2, 0);

                    if (2 <= emailParts.Length)
                    {
                        string[] userNameParts = emailParts[0].Split(EmailUserNamePartsSeparator, StringSplitOptions.RemoveEmptyEntries);

                        user = string.Join(" ", userNameParts.Select(FirstCharToUpper).ToArray());
                    }
                }
            }

            return user;
        }

        private string GetEmail(
            string user)
        {
            string email = user.ToLower().Replace(' ', '.') + "@" + EmailDomain;

            if (mUserToEmailDictionary != null)
            {
                email = mUserToEmailDictionary.GetValue(user) ?? email;
            }

            return email;
        }

        private string GetTagFromLabel(
            string label)
        {
            // git tag names must be valid filenames, so replace sequences of
            // invalid characters with an underscore
            string baseTag = GetGitTagToFileNameReplacementRegex().Replace(label, "_");

            // git tags are global, whereas VSS tags are local, so ensure
            // global uniqueness by appending a number; since the file system
            // may be case-insensitive, ignore case when hashing tags
            string tag = baseTag;
            for (int i = 2; !mUsedTags.Add(tag.ToUpperInvariant()); ++i)
            {
                tag = baseTag + "-" + i;
            }

            return tag;
        }

        void IGitStatistic.AddCommit()
        {
            ++mStatisticCommitCount;
        }

        void IGitStatistic.AddTag()
        {
            ++mStatisticTagCount;
        }

        [GeneratedRegex("[^A-Za-z0-9_-]+")]
        private static partial Regex GetGitTagToFileNameReplacementRegex();
    };
}
