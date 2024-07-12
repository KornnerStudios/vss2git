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
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;
using Hpdi.VssLogicalLib;
using Hpdi.VssPhysicalLib;
using SourceSafe;
using SourceSafe.Logical;

namespace Hpdi.Vss2Git
{
    /// <summary>
    /// Replays and commits changesets into a new repository.
    /// </summary>
    /// <author>Trevor Robinson</author>
    sealed partial class GitExporter : Worker, IGitStatistic
    {
        public static bool DryRunOutputTargetWorkDirPathsAsRelative { get; set; }
            = true;

        private readonly VssDatabase database;
        private readonly RevisionAnalyzer revisionAnalyzer;
        private readonly ChangesetBuilder changesetBuilder;
        private readonly HashSet<string> tagsUsed = [];
        private EmailDictionaryFileReader userToEmailDictionary = null;
        private readonly HashSet<string> excludedProjects = [];
        private readonly HashSet<string> excludedFiles =  [];
        private int commitCount = 0;
        private int tagCount = 0;
        PathMatcher vssProjectInclusionMatcher = null;
        PathMatcher fileExclusionMatcher = null;

        public string EmailDomain { get; set; } = "localhost";
        public bool ResetRepo { get; set; } = true;
        public bool ForceAnnotatedTags { get; set; } = true;

        public bool IgnoreErrors { get; set; } = false;

        public int LoggerAutoFlushOnChangesetInterval { get; set; } = 64;
        public bool IncludeIgnoredFiles { get; set; }

        public bool DryRun { get; set; } = false;

        public string UserToEmailDictionaryFile
        {
            set
            {
                userToEmailDictionary = new EmailDictionaryFileReader( value );
            }
        }

        public bool IncludeVssMetaDataInComments { get; set; } = false;
        public string VssIncludedProjects { get; set; }
        public string ExcludeFiles { get; set; }

        public string DefaultComment { get; set; } = "";

        internal static readonly char[] EmailPartsSeparator = ['@'];
        internal static readonly char[] EmailUserNamePartsSeparator = ['.'];

        public GitExporter(WorkQueue workQueue, Logger logger,
            RevisionAnalyzer revisionAnalyzer, ChangesetBuilder changesetBuilder)
            : base(workQueue, logger)
        {
            this.database = revisionAnalyzer.Database;
            this.revisionAnalyzer = revisionAnalyzer;
            this.changesetBuilder = changesetBuilder;
        }

        public void ExportToGit(IGitWrapper git)
        {
            if (!string.IsNullOrEmpty(VssIncludedProjects))
            {
                string[] includeProjectArray = VssIncludedProjects.Split(
                    new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
                vssProjectInclusionMatcher = new PathMatcher(includeProjectArray);
            }

            if (!string.IsNullOrEmpty(ExcludeFiles))
            {
                string[] excludeFileArray = ExcludeFiles.Split(
                    new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
                fileExclusionMatcher = new PathMatcher(excludeFileArray);
            }

            workQueue.AddLast(delegate(object work)
            {
                var stopwatch = Stopwatch.StartNew();

                logger.WriteSectionSeparator();
                LogStatus(work, "Initializing repository");

                if (DryRun && DryRunOutputTargetWorkDirPathsAsRelative)
                {
                    logger.WriteLine($"\t{VssPathMapper.RelativeWorkDirPrefix} = {git.GetRepoPath()}");
                }

                if (!string.IsNullOrEmpty(ExcludeFiles))
                {
                    logger.WriteLine("\tExcluded projects/files: {0}", ExcludeFiles);
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
                            workQueue.Abort();
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
                foreach (VssProject rootProject in revisionAnalyzer.RootProjects)
                {
                    // root must be repo path here - the path mapper uses paths relative to this one
                    string rootPath = git.GetRepoPath();
                    pathMapper.SetProjectPath(rootProject.PhysicalName, rootPath, rootProject.LogicalPath);
                }

                if (DryRun && changesetBuilder.ChangesetsWithMeaningfulComments.Count > 0)
                {
                    logger.WriteSectionSeparator();

                    float meaningfulCommentPercentage = changesetBuilder.ChangesetsWithMeaningfulComments.Count / (float)changesetBuilder.Changesets.Count;
                    meaningfulCommentPercentage *= 100.0f;

                    logger.WriteLine($"Changesets with meaningful comments ({meaningfulCommentPercentage}%):");
                    foreach (Changeset changeset in changesetBuilder.ChangesetsWithMeaningfulComments)
                    {
                        logger.WriteLine($"\tChangeset {changeset.Id} {changeset.DateTime} {changeset.User}");
                        foreach (string commentLine in changeset.Comment)
                        {
                            logger.WriteLine($"\t\t{commentLine}");
                        }
                    }
                    logger.Flush();
                }

                // replay each changeset
                int changesetId = 1;
                List<Changeset> changesets = changesetBuilder.Changesets;
                excludedProjects.Clear();
                excludedFiles.Clear();
                commitCount = 0;
                tagCount = 0;
                var replayStopwatch = new Stopwatch();
                var gitStopwatch = new Stopwatch();
                tagsUsed.Clear();
                foreach (Changeset changeset in changesets)
                {
                    if (LoggerAutoFlushOnChangesetInterval > 0 && (changesetId % LoggerAutoFlushOnChangesetInterval) == 0)
                    {
                        logger.Flush();
                    }

                    string changesetDesc = string.Format(CultureInfo.InvariantCulture,
                        "changeset {0} from {1}", changesetId, VssDatabase.FormatISOTimestamp(changeset.DateTime));

                    logger.WriteLine("//-------------------------------------------------------------------------//");

                    // replay each revision in changeset
                    LogStatus(work, "Replaying " + changesetDesc);

                    var pendingCommits = new Dictionary<string, GitActions.Commit>();

                    replayStopwatch.Start();
                    try
                    {
                        ReplayChangeset(pathMapper, changeset, ref pendingCommits);
                    }
                    finally
                    {
                        replayStopwatch.Stop();
                    }

                    if (workQueue.IsAborting)
                    {
                        return;
                    }

                    if (!DryRun)
                    {
                        // commit changes
                        LogStatus(work, "Committing " + changesetDesc);

                        gitStopwatch.Start();
                        try
                        {
                            IOrderedEnumerable<KeyValuePair<string, GitActions.Commit>> pendingCommitsOrderedByBranch = pendingCommits.OrderBy(x => x.Key);

                            foreach (KeyValuePair<string, GitActions.Commit> commit in pendingCommitsOrderedByBranch)
                            {
                                commit.Value.Run(logger, git, this);

                                if (workQueue.IsAborting)
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

                    if (workQueue.IsAborting)
                    {
                        return;
                    }

                    ++changesetId;
                }

                stopwatch.Stop();

                logger.WriteSectionSeparator();
                logger.WriteLine("Vss Projects : {0} excluded", excludedProjects.Count);
                logger.WriteLine("Vss Files: {0} excluded", excludedFiles.Count);
                logger.WriteLine("Git export complete in {0:HH:mm:ss}", new DateTime(stopwatch.ElapsedTicks));
                logger.WriteLine("Replay time: {0:HH:mm:ss}", new DateTime(replayStopwatch.ElapsedTicks));
                if (!DryRun)
                {
                    logger.WriteLine("Git time: {0:HH:mm:ss}", new DateTime(gitStopwatch.ElapsedTicks));
                }
                logger.WriteLine("Git commits: {0}", commitCount);
                logger.WriteLine("Git tags: {0}", tagCount);
            });
        }
        private void ReplayChangeset(VssPathMapper pathMapper, Changeset changeset, ref Dictionary<string, GitActions.Commit> pendingCommits)
        {
            foreach (Revision revision in changeset.Revisions)
            {
                ReplayRevision(pathMapper, changeset, revision, ref pendingCommits);
            }
        }

        private void ReplayRevision(VssPathMapper pathMapper, Changeset changeset, Revision revision, ref Dictionary<string, GitActions.Commit> pendingCommits)
        {
            string indentStr = VssRecord.DumpGetIndentString(1);

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
                List<string> projectLogicalPath = pathMapper.GetProjectLogicalPath(project.PhysicalName);
                List<string> projectWorkDirPath = pathMapper.GetProjectWorkDirPath(project.PhysicalName);

                VssItemName target = null;
                List<string> targetWorkDirPath = null;
                List<string> targetLogicalPath = null;

                if (projectLogicalPath == null && projectWorkDirPath == null && actionType == VssActionType.Create)
                {
                    if (revision.Action is VssNamedAction namedAction)
                    {
                        target = namedAction.Name;
                    }

                    if (null != target)
                    {
                        logger.Write(indentStr);
                        logger.WriteLine("{0}: {1} {2}", revision.Item.ToString(), actionType, target.LogicalName);
                        pathMapper.CreateItem(project);
                        projectLogicalPath = pathMapper.GetProjectLogicalPath(project.PhysicalName);
                        projectWorkDirPath = pathMapper.GetProjectWorkDirPath(project.PhysicalName);
                    }
                }
                else
                {
                    if (revision.Action is VssNamedAction namedAction)
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
                VssItemInfo itemInfo = null;
                switch (actionType)
                {
                    case VssActionType.Label:
                    {
                        string labelName = ((VssLabelAction)revision.Action).Label;

                        if (string.IsNullOrEmpty(labelName))
                        {
                            logger.Write(indentStr);
                            logger.WriteLine("NOTE: Ignoring empty label");
                        }
                        else if (ShouldLogicalPathBeIncluded(pathMapper, true, projectLogicalPath))
                        {
                            GitActions.Commit pendingCommit = GetOrCreatePendingCommitForProject(ref pendingCommits, changeset, pathMapper, project);

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
                        GitActions.Commit pendingCommit = GetOrCreatePendingCommitForProject(ref pendingCommits, changeset, pathMapper, project);
                        VssUtil.MarkUnusedVariable(ref pendingCommit);

                        string projectDesc = GetProjectDescription(pathMapper, revision, project, projectLogicalPath);

                        logger.Write(indentStr);
                        logger.WriteLine("{0}: {1} {2}", projectDesc, actionType, target.LogicalName);

                        itemInfo = pathMapper.AddItem(project, target, revisionAnalyzer.IsDestroyed(project.PhysicalName, target.PhysicalName));

                        isAddAction = ShouldLogicalPathBeIncluded(pathMapper, target.IsProject, targetLogicalPath ?? projectLogicalPath);

                        break;
                    }

                    case VssActionType.Share:
                    {
                        var shareAction = (VssShareAction)revision.Action;

                        GitActions.Commit pendingCommit = GetOrCreatePendingCommitForProject(ref pendingCommits, changeset, pathMapper, project);
                        VssUtil.MarkUnusedVariable(ref pendingCommit);

                        string projectDesc = GetProjectDescription(pathMapper, revision, project, projectLogicalPath);

                        logger.Write(indentStr);
                        if (shareAction.Pinned)
                        {
                            logger.WriteLine($"{projectDesc}: {actionType} {target.LogicalName}, Pin {shareAction.Revision}");
                            itemInfo = pathMapper.AddItem(project, target, revisionAnalyzer.IsDestroyed(project.PhysicalName, target.PhysicalName), shareAction.Revision);
                        }
                        else
                        {
                            logger.WriteLine($"{projectDesc}: {actionType} {target.LogicalName}");
                            itemInfo = pathMapper.AddItem(project, target, revisionAnalyzer.IsDestroyed(project.PhysicalName, target.PhysicalName));
                        }

                        isAddAction = ShouldLogicalPathBeIncluded(pathMapper, target.IsProject, targetLogicalPath);

                        break;
                    }

                    case VssActionType.Recover:
                    {
                        GitActions.Commit pendingCommit = GetOrCreatePendingCommitForProject(ref pendingCommits, changeset, pathMapper, project);
                        VssUtil.MarkUnusedVariable(ref pendingCommit);

                        string projectDesc = GetProjectDescription(pathMapper, revision, project, projectLogicalPath);

                        logger.Write(indentStr);
                        logger.WriteLine($"{projectDesc}: {actionType} {target.LogicalName}");

                        itemInfo = pathMapper.RecoverItem(project, target, revisionAnalyzer.IsDestroyed(project.PhysicalName, target.PhysicalName));

                        isAddAction = ShouldLogicalPathBeIncluded(pathMapper, target.IsProject, targetLogicalPath);

                        break;
                    }

                    case VssActionType.Delete:
                    case VssActionType.Destroy:
                    {
                        GitActions.Commit pendingCommit = GetOrCreatePendingCommitForProject(ref pendingCommits, changeset, pathMapper, project);

                        string projectDesc = GetProjectDescription(pathMapper, revision, project, projectLogicalPath);

                        logger.Write(indentStr);
                        logger.WriteLine($"{projectDesc}: {actionType} {target.LogicalName}");

                        itemInfo = pathMapper.DeleteItem(project, target, (VssActionType.Destroy == actionType));

                        if (targetLogicalPath != null && targetWorkDirPath != null)
                        {
                            if (ShouldLogicalPathBeIncluded(pathMapper, target.IsProject, targetLogicalPath))
                            {
                                string targetWorkDirPathAsString = VssPathMapper.WorkDirPathToString(targetWorkDirPath);

                                if (target.IsProject)
                                {
                                    var projectInfo = (VssProjectInfo)itemInfo;

                                    if (VssActionType.Destroy == actionType || !projectInfo.Destroyed)
                                    {
                                        bool containsFiles = projectInfo.ContainsFiles();

                                        pendingCommit.AddAction(revision, new GitActions.DeleteDirectory(targetWorkDirPathAsString, containsFiles), containsFiles);
                                    }
                                    else
                                    {
                                        logger.Write(indentStr);
                                        logger.WriteLine("NOTE: Skipping move to because {0} in project {1} is destroyed",
                                            target.LogicalName, VssPathMapper.LogicalPathToString(projectLogicalPath));
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
                                            logger.Write(indentStr);
                                            logger.WriteLine("NOTE: {0} contains another file named {1}; not deleting file",
                                                projectDesc, target.LogicalName);
                                        }
                                        else
                                        {
                                            string targetLogicalPathAsString = VssPathMapper.LogicalPathToString(targetLogicalPath);

                                            pendingCommit.DeleteFileOrDirectory(revision, new GitActions.DeleteFile(targetWorkDirPathAsString), targetLogicalPathAsString);
                                        }
                                    }
                                    else
                                    {
                                        logger.Write(indentStr);
                                        logger.WriteLine("NOTE: Skipping move to because {0} in project {1} is destroyed",
                                            target.LogicalName, VssPathMapper.LogicalPathToString(projectLogicalPath));
                                    }
                                }
                            }
                        }

                        break;
                    }

                    case VssActionType.Rename:
                    {
                        var renameAction = (VssRenameAction)revision.Action;

                        GitActions.Commit pendingCommit = GetOrCreatePendingCommitForProject(ref pendingCommits, changeset, pathMapper, project);

                        string projectDesc = GetProjectDescription(pathMapper, revision, project, projectLogicalPath);

                        logger.Write(indentStr);
                        logger.WriteLine($"{projectDesc}: {actionType} {renameAction.OriginalName} to {target.LogicalName}");

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

                                bool destroyed = target.IsProject ? ((VssProjectInfo)itemInfo).Destroyed : pathMapper.GetFileDestroyed(project, target.PhysicalName);

                                if (!destroyed)
                                {
                                    // renaming a file or a project that contains files?
                                    if (itemInfo is not VssProjectInfo projectInfo || projectInfo.ContainsFiles())
                                    {
                                        if (!DryRun)
                                        {
                                            string sourceLogicalPathAsString = VssPathMapper.LogicalPathToString(sourceLogicalPath);
                                            string targetLogicalPathAsString = VssPathMapper.LogicalPathToString(targetLogicalPath);

                                            string message = $"{targetLogicalPathAsString} (from {sourceLogicalPathAsString})";

                                            if (target.IsProject)
                                            {
                                                pendingCommit.MoveFileOrDirectory(revision, new GitActions.MoveDirectory(sourceWorkDirPathAsString, targetWorkDirPathAsString, true), message);
                                            }
                                            else
                                            {
                                                pendingCommit.MoveFileOrDirectory(revision, new GitActions.MoveFile(sourceWorkDirPathAsString, targetWorkDirPathAsString), message);
                                            }
                                        }
                                    }
                                    else
                                    {
                                        // git doesn't care about directories with no files
                                        pendingCommit.AddAction(revision, new GitActions.MoveDirectory(sourceWorkDirPathAsString, targetWorkDirPathAsString, false), false);
                                    }
                                }
                                else
                                {
                                    logger.Write(indentStr);
                                    logger.WriteLine("NOTE: Skipping move to because {0} in project {1} is destroyed",
                                        target.LogicalName, VssPathMapper.LogicalPathToString(projectLogicalPath));
                                }
                            }
                        }

                        break;
                    }

                    case VssActionType.MoveFrom:
                    {
                        // if both MoveFrom & MoveTo are present (e.g.
                        // one of them has not been destroyed), only one
                        // can succeed, so check that the source exists

                        var moveFromAction = (VssMoveFromAction)revision.Action;

                        GitActions.Commit pendingCommit = GetOrCreatePendingCommitForProject(ref pendingCommits, changeset, pathMapper, project);

                        string projectDesc = GetProjectDescription(pathMapper, revision, project, projectLogicalPath);

                        string targetLogicalPathAsString = VssPathMapper.LogicalPathToString(targetLogicalPath);

                        logger.Write(indentStr);
                        logger.WriteLine($"{projectDesc}: Move from {moveFromAction.OriginalProject} to {targetLogicalPathAsString}");

                        if (pathMapper.IsProjectRooted(moveFromAction.Name.PhysicalName))
                        {
                            List<string> sourceLogicalPath = pathMapper.GetProjectLogicalPath(moveFromAction.Name.PhysicalName);
                            List<string> sourceWorkDirPath = pathMapper.GetProjectWorkDirPath(moveFromAction.Name.PhysicalName);

                            VssProjectInfo projectInfo = pathMapper.MoveProjectFrom(project, target, moveFromAction.OriginalProject);

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

                                                pendingCommit.MoveFileOrDirectory(revision, new GitActions.MoveDirectory(sourceWorkDirPathAsString, targetWorkDirPathAsString, true), message);
                                            }
                                            else
                                            {
                                                pendingCommit.AddAction(revision, new GitActions.MoveDirectory(sourceWorkDirPathAsString, targetWorkDirPathAsString, false), false);
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
                            else if (null != projectLogicalPath && projectInfo.Destroyed)
                            {
                                logger.Write(indentStr);
                                logger.WriteLine("NOTE: Skipping move to because {0} is destroyed",
                                    VssPathMapper.LogicalPathToString(projectLogicalPath));
                            }
                        }
                        else
                        {
                            // the moved project is currently not part of project tree

                            bool destroyed = revisionAnalyzer.IsDestroyed(project.PhysicalName, target.PhysicalName);

                            if (!destroyed)
                            {
                                logger.Write(indentStr);
                                logger.WriteLine("NOTE: Moving unmapped project {0} from {1} to {2}",
                                    target.LogicalName, moveFromAction.OriginalProject, VssPathMapper.LogicalPathToString(projectLogicalPath));

                                // add it again
                                itemInfo = pathMapper.AddItem(project, target, destroyed);

                                isAddAction = ShouldLogicalPathBeIncluded(pathMapper, target.IsProject, targetLogicalPath);
                            }
                            else
                            {
                                logger.Write(indentStr);
                                logger.WriteLine("NOTE: Skipping move to because {0} in project {1} is destroyed",
                                    target.LogicalName, VssPathMapper.LogicalPathToString(projectLogicalPath));
                            }
                        }

                        break;
                    }

                    case VssActionType.MoveTo:
                    {
                        // handle actual moves in MoveFrom; this just does cleanup of destroyed projects
                        var moveToAction = (VssMoveToAction)revision.Action;

                        GitActions.Commit pendingCommit = GetOrCreatePendingCommitForProject(ref pendingCommits, changeset, pathMapper, project);

                        string projectDesc = GetProjectDescription(pathMapper, revision, project, projectLogicalPath);

                        string targetLogicalPathAsString = VssPathMapper.LogicalPathToString(targetLogicalPath);

                        logger.WriteLine($"{projectDesc}: Move to {moveToAction.NewProject} from {targetLogicalPathAsString}");

                        VssProjectInfo projectInfo = pathMapper.MoveProjectTo(project, target, moveToAction.NewProject);

                        if (!projectInfo.Destroyed && targetLogicalPath != null && targetWorkDirPath != null)
                        {
                            if (ShouldLogicalPathBeIncluded(pathMapper, target.IsProject, targetLogicalPath))
                            {
                            string targetWorkDirPathAsString = VssPathMapper.WorkDirPathToString(targetWorkDirPath);

                                // project was moved to a now-destroyed project; remove empty directory
                                pendingCommit.AddAction(revision, new GitActions.DeleteDirectory(targetWorkDirPathAsString, false), false);
                            }
                        }

                        break;
                    }

                    case VssActionType.Pin:
                    {
                        GitActions.Commit pendingCommit = GetOrCreatePendingCommitForProject(ref pendingCommits, changeset, pathMapper, project);
                        VssUtil.MarkUnusedVariable(ref pendingCommit);

                        string projectDesc = GetProjectDescription(pathMapper, revision, project, projectLogicalPath);

                        bool destroyed = revisionAnalyzer.IsDestroyed(project.PhysicalName, target.PhysicalName);
                        var pinAction = (VssPinAction)revision.Action;

                        logger.Write(indentStr);
                        if (pinAction.Pinned)
                        {
                            logger.WriteLine($"{projectDesc}: Pin {target.LogicalName}");
                            itemInfo = pathMapper.PinItem(project, target, pinAction.Revision, destroyed);
                            writeFile = !destroyed && ShouldLogicalPathBeIncluded(pathMapper, target.IsProject, targetLogicalPath);
                        }
                        else
                        {
                            logger.WriteLine($"{projectDesc}: Unpin {target.LogicalName}");
                            itemInfo = pathMapper.UnpinItem(project, target, destroyed);
                            writeFile = !destroyed && ShouldLogicalPathBeIncluded(pathMapper, target.IsProject, targetLogicalPath);
                        }

                        break;
                    }

                    case VssActionType.Branch:
                    {
                        GitActions.Commit pendingCommit = GetOrCreatePendingCommitForProject(ref pendingCommits, changeset, pathMapper, project);
                        VssUtil.MarkUnusedVariable(ref pendingCommit);

                        string projectDesc = GetProjectDescription(pathMapper, revision, project, projectLogicalPath);

                        var branchAction = (VssBranchAction)revision.Action;
                        logger.Write(indentStr);
                        logger.WriteLine($"{projectDesc}: {actionType} {target.LogicalName}");

                        itemInfo = pathMapper.BranchFile(project, target, branchAction.Source, revisionAnalyzer.IsDestroyed(project.PhysicalName, target.PhysicalName));

                        break;
                    }

                    // currently ignored
                    case VssActionType.Archive:
                    {
                        GitActions.Commit pendingCommit = GetOrCreatePendingCommitForProject(ref pendingCommits, changeset, pathMapper, project);
                        VssUtil.MarkUnusedVariable(ref pendingCommit);

                        string projectDesc = GetProjectDescription(pathMapper, revision, project, projectLogicalPath);

                        var archiveAction = (VssArchiveAction)revision.Action;
                        logger.Write(indentStr);
                        logger.WriteLine($"{projectDesc}: Archive {target.LogicalName} to {archiveAction.ArchivePath} (ignored)");

                        break;
                    }

                    case VssActionType.Restore:
                    {
                        GitActions.Commit pendingCommit = GetOrCreatePendingCommitForProject(ref pendingCommits, changeset, pathMapper, project);
                        VssUtil.MarkUnusedVariable(ref pendingCommit);

                        string projectDesc = GetProjectDescription(pathMapper, revision, project, projectLogicalPath);

                        var restoreAction = (VssRestoreAction)revision.Action;
                        logger.Write(indentStr);
                        logger.WriteLine($"{projectDesc}: Restore {target.LogicalName} from archive {restoreAction.ArchivePath}");

                        itemInfo = pathMapper.AddItem(project, target, revisionAnalyzer.IsDestroyed(project.PhysicalName, target.PhysicalName));
                        isAddAction =  ShouldLogicalPathBeIncluded(pathMapper, target.IsProject, targetLogicalPath);

                        break;
                    }
                }

                if (targetLogicalPath != null && targetWorkDirPath != null)
                {
                    if (isAddAction)
                    {
                        if (target.IsProject)
                        {
                            if (ShouldLogicalPathBeIncluded(pathMapper, true, targetLogicalPath))
                            {
                                GitActions.Commit pendingCommit = GetOrCreatePendingCommitForProject(ref pendingCommits, changeset, pathMapper, project);

                                var projectInfo = (VssProjectInfo)itemInfo;

                                if (!projectInfo.Destroyed)
                                {
                                    string targetWorkDirPathAsString = VssPathMapper.WorkDirPathToString(targetWorkDirPath);
                                    pendingCommit.AddAction(revision, new GitActions.CreateDirectory(targetWorkDirPathAsString), false);
                                    writeProject = true;
                                }
                                else
                                {
                                    string targetLogicalPathAsString = VssPathMapper.WorkDirPathToString(targetLogicalPath);
                                    logger.Write(indentStr);
                                    logger.WriteLine($"NOTE: Skipping destroyed project: {targetLogicalPathAsString}");
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
                        foreach (VssProjectInfo projectInfo in pathMapper.GetAllProjects(target.PhysicalName))
                        {
                            if (!ShouldLogicalPathBeIncluded(pathMapper, true, projectInfo.GetLogicalPath()))
                            {
                                continue;
                            }

                            GitActions.Commit pendingCommit = GetOrCreatePendingCommitForProject(ref pendingCommits, changeset, pathMapper, project);

                            string projectDesc = GetProjectDescription(pathMapper, revision, project, projectLogicalPath);

                            if (DryRun)
                            {
                                logger.Write(indentStr);
                                logger.WriteLine($"{projectDesc}: Creating subdirectory {projectInfo.LogicalName}");
                            }

                            string targetWorkDirPathAsString = VssPathMapper.WorkDirPathToString(projectInfo.GetWorkDirPath());

                            pendingCommit.AddAction(revision, new GitActions.CreateDirectory(targetWorkDirPathAsString), false);
                        }

                        // write current rev of all contained files
                        foreach (VssFileInfo fileInfo in pathMapper.GetAllFiles(target.PhysicalName))
                        {
                            IEnumerable<Tuple<List<string>, List<string>>> paths = pathMapper.GetFilePaths(fileInfo.PhysicalName, target.PhysicalName, revision.Version, logger);

                            foreach (Tuple<List<string>, List<string>> p in paths)
                            {
                                if (!ShouldLogicalPathBeIncluded(pathMapper, false, p.Item1))
                                {
                                    continue;
                                }

                                GitActions.Commit pendingCommit = GetOrCreatePendingCommitForLogicalPath(ref pendingCommits, changeset, p.Item1);

                                string targetWorkDirPathAsString = VssPathMapper.WorkDirPathToString(p.Item2);

                                if (DryRun)
                                {
                                    string targetWorkDirPathAsStringForDryRun = DryRunOutputTargetWorkDirPathsAsRelative
                                        ? VssPathMapper.RelativeWorkDirPathToString(p.Item2)
                                        : targetWorkDirPathAsString;

                                    logger.Write(indentStr);
                                    logger.WriteLine($"{targetWorkDirPathAsStringForDryRun}: {actionType} revision {fileInfo.Version}");
                                }

                                pendingCommit.AddFile(revision, new GitActions.WriteFile(database, fileInfo.PhysicalName, fileInfo.Version, targetWorkDirPathAsString), VssPathMapper.LogicalPathToString(p.Item1));
                            }
                        }
                    }
                    else if (writeFile)
                    {
                        if (ShouldLogicalPathBeIncluded(pathMapper, false, targetLogicalPath))
                        {
                            bool destroyed = pathMapper.GetFileDestroyed(project, target.PhysicalName);

                            GitActions.Commit pendingCommit = GetOrCreatePendingCommitForLogicalPath(ref pendingCommits, changeset, targetLogicalPath);

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

                                    logger.Write(indentStr);
                                    logger.WriteLine($"{targetWorkDirPathAsStringForDryRun}: {actionType} revision {version}");
                                }

                                pendingCommit.AddFile(revision, new GitActions.WriteFile(database, target.PhysicalName, version, targetWorkDirPathAsString), targetLogicalPathAsString);
                            }
                            else
                            {
                                logger.Write(indentStr);
                                logger.WriteLine($"NOTE: Skipping destroyed file: {targetLogicalPathAsString}");
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
                IEnumerable<Tuple<List<string>, List<string>>> paths = pathMapper.GetFilePaths(target.PhysicalName, null, revision.Version, logger);

                foreach (Tuple<List<string>, List<string>> p in paths)
                {
                    if (!ShouldLogicalPathBeIncluded(pathMapper, false, p.Item1))
                    {
                        continue;
                    }

                    GitActions.Commit pendingCommit = GetOrCreatePendingCommitForLogicalPath(ref pendingCommits, changeset, p.Item1);

                    string targetWorkDirPathAsString = VssPathMapper.WorkDirPathToString(p.Item2);

                    if (DryRun)
                    {
                        string targetWorkDirPathAsStringForDryRun = DryRunOutputTargetWorkDirPathsAsRelative
                            ? VssPathMapper.RelativeWorkDirPathToString(p.Item2)
                            : targetWorkDirPathAsString;

                        logger.Write(indentStr);
                        logger.WriteLine($"{targetWorkDirPathAsStringForDryRun}: {actionType} revision {revision.Version}");
                    }

                    pendingCommit.WriteFile(revision, new GitActions.WriteFile(database, target.PhysicalName, revision.Version, targetWorkDirPathAsString));
                }
            }
        }

        private bool ShouldLogicalPathBeIncluded(VssPathMapper _/*pathMapper*/, bool isProject, List<string> logicalPath)
        {
            bool shouldBeIncluded = false;

            if (null != logicalPath)
            {
                string path = VssPathMapper.LogicalPathToString(logicalPath);

                if (null != vssProjectInclusionMatcher)
                {
                    if (vssProjectInclusionMatcher.Matches(path))
                    {
                        if (fileExclusionMatcher == null || !fileExclusionMatcher.Matches(path))
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
                            logger.Write(VssRecord.DumpGetIndentString(1));
                            logger.WriteLine($"Excluding project {path}");
                        }
                        excludedProjects.Add(path);
                    }
                    else
                    {
                        if (DryRun)
                        {
                            logger.Write(VssRecord.DumpGetIndentString(1));
                            logger.WriteLine($"Excluding file {path}");
                        }
                        excludedFiles.Add(path);
                    }
                }
            }
            else
            {
                shouldBeIncluded = false;
            }

            return shouldBeIncluded;
        }

        private string GetProjectDescription(VssPathMapper _/*pathMapper*/, Revision revision, VssItemName project, List<string> projectPath)
        {
            string projectDesc;

            if (null != projectPath)
            {
                projectDesc = VssPathMapper.LogicalPathToString(projectPath);
            }
            else
            {
                projectDesc = revision.Item.ToString();

                logger.Write(VssRecord.DumpGetIndentString(1));
                logger.WriteLine($"NOTE: {project.LogicalName} is currently unmapped");
            }

            return projectDesc;
        }

        private GitActions.Commit GetOrCreatePendingCommitForLogicalPath(
            ref Dictionary<string, GitActions.Commit> pendingCommits,
            Changeset changeset,
            List<string> _/*logicalPath*/)
        {
            // #REVIEW this needs to be more robust, but we cannot call IGitWrapper.GetDefaultBranch() from here
            const string pendingBranch = AbstractGitWrapper.DefaultCheckoutBranch;

            if (!pendingCommits.TryGetValue(pendingBranch, out GitActions.Commit pendingCommit))
            {
                pendingCommit = CreateGitCommitAction(changeset);
                pendingCommits[pendingBranch] = pendingCommit;
            }

            return pendingCommit;
        }

        private GitActions.Commit GetOrCreatePendingCommitForProject(
            ref Dictionary<string, GitActions.Commit> pendingCommits,
            Changeset changeset,
            VssPathMapper pathMapper,
            VssItemName project)
        {
            return GetOrCreatePendingCommitForLogicalPath(ref pendingCommits, changeset, pathMapper.GetProjectLogicalPath(project.PhysicalName));
        }

        [System.Obsolete("Unused")]
        private static string GetPendingBranchFromLogicalProjectPath(List<string> logicalProjectPath)
        {
            string pendingBranch = "";

            if (null != logicalProjectPath && 2 <= logicalProjectPath.Count && SourceSafe.SourceSafeConstants.RootProjectName == logicalProjectPath[0])
            {
                pendingBranch = logicalProjectPath[1];
            }

            return pendingBranch;
        }

        private GitActions.Commit CreateGitCommitAction(Changeset changeset)
        {
            return new GitActions.Commit(changeset, GetUser(changeset.User), GetEmail(changeset.User), changeset.DateTime.ConvertAmbiguousTimeToUtc(logger), IncludeVssMetaDataInComments);
        }

        private GitActions.CreateTag CreateGitTagAction(string labelName, string user, string comment, DateTime localTime)
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

            return new GitActions.CreateTag(tagName, GetUser(user), GetEmail(user), tagMessage, localTime.ConvertAmbiguousTimeToUtc(logger));
        }

        private bool RetryCancel(ThreadStart work)
        {
            // #TODO WinForms - this isn't CLI-ready
            return AbortRetryIgnore(work, MessageBoxButtons.RetryCancel);
        }

        private bool AbortRetryIgnore(ThreadStart work)
        {
            // #TODO WinForms - this isn't CLI-ready
            return AbortRetryIgnore(work, MessageBoxButtons.AbortRetryIgnore);
        }

        // #TODO WinForms - this isn't CLI-ready
        private bool AbortRetryIgnore(ThreadStart work, MessageBoxButtons buttons)
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
                            workQueue.Abort();
                            break;
                    }
                }
            } while (retry);
            return false;
        }

        private static string FirstCharToUpper(string input)
        {
            if (string.IsNullOrEmpty(input))
                throw new ArgumentException($"{nameof(input)} cannot be empty", nameof(input));
            return string.Concat(input.First().ToString().ToUpper(), input.AsSpan(1));
        }

        private string GetUser(string user)
        {
            if (null != userToEmailDictionary)
            {
                string email = userToEmailDictionary.GetValue(user, null);

                if (null != email)
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

        private string GetEmail(string user)
        {
            string email = user.ToLower().Replace(' ', '.') + "@" + EmailDomain;

            if (null != userToEmailDictionary)
            {
                email = userToEmailDictionary.GetValue(user, email);
            }

            return email;
        }

        private string GetTagFromLabel(string label)
        {
            // git tag names must be valid filenames, so replace sequences of
            // invalid characters with an underscore
            string baseTag = GetGitTagToFileNameReplacementRegex().Replace(label, "_");

            // git tags are global, whereas VSS tags are local, so ensure
            // global uniqueness by appending a number; since the file system
            // may be case-insensitive, ignore case when hashing tags
            string tag = baseTag;
            for (int i = 2; !tagsUsed.Add(tag.ToUpperInvariant()); ++i)
            {
                tag = baseTag + "-" + i;
            }

            return tag;
        }

        public void AddCommit()
        {
            ++commitCount;
        }

        public void AddTag()
        {
            ++tagCount;
        }

        [GeneratedRegex("[^A-Za-z0-9_-]+")]
        private static partial Regex GetGitTagToFileNameReplacementRegex();
    }
}
