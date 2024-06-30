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

namespace Hpdi.Vss2Git
{
    /// <summary>
    /// Replays and commits changesets into a new repository.
    /// </summary>
    /// <author>Trevor Robinson</author>
    class GitExporter : Worker, IGitStatistic
    {
        private readonly VssDatabase database;
        private readonly RevisionAnalyzer revisionAnalyzer;
        private readonly ChangesetBuilder changesetBuilder;
        private readonly HashSet<string> tagsUsed = new HashSet<string>();
        private bool ignoreErrors = false;
        private bool dryRun = false;
        private EmailDictionaryFileReader userToEmailDictionary = null;
        private bool includeVssMetaDataInComments = false;
        private HashSet<string> excludedProjects = new HashSet<string>();
        private HashSet<string> excludedFiles =  new HashSet<string>();
        private int commitCount = 0;
        private int tagCount = 0;
        PathMatcher vssProjectInclusionMatcher = null;
        PathMatcher fileExclusionMatcher = null;
        private string defaultComment = "";

        private string emailDomain = "localhost";
        public string EmailDomain
        {
            get { return emailDomain; }
            set { emailDomain = value; }
        }

        private bool resetRepo = true;
        public bool ResetRepo
        {
            get { return resetRepo; }
            set { resetRepo = value; }
        }

        private bool forceAnnotatedTags = true;
        public bool ForceAnnotatedTags
        {
            get { return forceAnnotatedTags; }
            set { forceAnnotatedTags = value; }
        }

        public bool IgnoreErrors
        {
            get { return ignoreErrors; }
            set { ignoreErrors = value; }
        }

        public int LoggerAutoFlushOnChangesetInterval { get; set; } = 64;
        public bool IncludeIgnoredFiles { get; set; }

        public bool DryRun
        {
            get { return dryRun; }
            set { dryRun = value; }
        }

        public string UserToEmailDictionaryFile
        {
            set
            {
                userToEmailDictionary = new EmailDictionaryFileReader( value );
            }
        }

        public bool IncludeVssMetaDataInComments
        {
            get { return includeVssMetaDataInComments; }
            set { includeVssMetaDataInComments = value; }
        }

        private string vssIncludedProjects;
        public string VssIncludedProjects
        {
            get { return vssIncludedProjects; }
            set { vssIncludedProjects = value; }
        }

        private string excludeFiles;
        public string ExcludeFiles
        {
            get { return excludeFiles; }
            set { excludeFiles = value; }
        }

        public string DefaultComment
        {
            get { return defaultComment; }
            set { defaultComment = value; }
        }

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
            if (!string.IsNullOrEmpty(vssIncludedProjects))
            {
                string[] includeProjectArray = vssIncludedProjects.Split(
                    new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
                vssProjectInclusionMatcher = new PathMatcher(includeProjectArray);
            }

            if (!string.IsNullOrEmpty(excludeFiles))
            {
                string[] excludeFileArray = excludeFiles.Split(
                    new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
                fileExclusionMatcher = new PathMatcher(excludeFileArray);
            }

            workQueue.AddLast(delegate(object work)
            {
                var stopwatch = Stopwatch.StartNew();

                logger.WriteSectionSeparator();
                LogStatus(work, "Initializing repository");

                logger.WriteLine("Excluded projects/files: {0}", excludeFiles);

                // create repository directory if it does not exist
                if (!dryRun && !Directory.Exists(git.GetRepoPath()))
                {
                    Directory.CreateDirectory(git.GetRepoPath());
                }

                if (!dryRun)
                {
                    while (!git.FindExecutable())
                    {
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

                    if (!RetryCancel(delegate { git.Init(resetRepo); }))
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

                // replay each changeset
                int changesetId = 1;
                LinkedList<Changeset> changesets = changesetBuilder.Changesets;
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

                    Dictionary<string, GitActions.Commit> pendingCommits = new Dictionary<string, GitActions.Commit>();

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

                    if (!dryRun)
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
                logger.WriteLine("Vss Projects : {0} excluded", excludedProjects.Count());
                logger.WriteLine("Vss Files: {0} excluded", excludedFiles.Count());
                logger.WriteLine("Git export complete in {0:HH:mm:ss}", new DateTime(stopwatch.ElapsedTicks));
                logger.WriteLine("Replay time: {0:HH:mm:ss}", new DateTime(replayStopwatch.ElapsedTicks));
                if (!dryRun)
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
            VssActionType actionType = revision.Action.Type;
            if (revision.Item.IsProject)
            {
                // note that project path (and therefore target path) can be
                // null if a project was moved and its original location was
                // subsequently destroyed
                VssItemName project = revision.Item;
                string projectName = project.LogicalName;
                List<string> projectLogicalPath = pathMapper.GetProjectLogicalPath(project.PhysicalName);
                List<string> projectWorkDirPath = pathMapper.GetProjectWorkDirPath(project.PhysicalName);

                VssItemName target = null;
                List<string> targetWorkDirPath = null;
                List<string> targetLogicalPath = null;

                if (projectLogicalPath == null && projectWorkDirPath == null && actionType == VssActionType.Create)
                {
                    var namedAction = revision.Action as VssNamedAction;
                    if (namedAction != null)
                    {
                        target = namedAction.Name;
                    }

                    if (null != target)
                    {
                        logger.WriteLine("{0}: {1} {2}", revision.Item.ToString(), actionType, target.LogicalName);
                        pathMapper.CreateItem(project);
                        projectLogicalPath = pathMapper.GetProjectLogicalPath(project.PhysicalName);
                        projectWorkDirPath = pathMapper.GetProjectWorkDirPath(project.PhysicalName);
                    }
                }
                else
                {
                    var namedAction = revision.Action as VssNamedAction;
                    if (namedAction != null)
                    {
                        target = namedAction.Name;
                        if (projectLogicalPath != null)
                        {
                            targetLogicalPath = new List<string>(projectLogicalPath);
                            targetLogicalPath.Add(target.LogicalName);
                        }

                        if (projectWorkDirPath != null)
                        {
                            targetWorkDirPath = new List<string>(projectWorkDirPath);
                            targetWorkDirPath.Add(target.LogicalName);
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
                                logger.WriteLine("NOTE: Ignoring empty label");
                            }
                            else if (ShouldLogicalPathBeIncluded(pathMapper, true, projectLogicalPath))
                            {
                            GitActions.Commit pendingCommit = GetOrCreatePendingCommitForProject(ref pendingCommits, changeset, pathMapper, project);

                                // defer tagging until after commit
                                pendingCommit.AddTag(revision, CreateGitTagAction(labelName, revision.User, revision.Comment, revision.DateTime));
                            }
                        }
                        break;

                    case VssActionType.Create:
                        {
                            // ignored; items are actually created when added to a project
                        }
                        break;

                    case VssActionType.Add:
                        {
                        GitActions.Commit pendingCommit = GetOrCreatePendingCommitForProject(ref pendingCommits, changeset, pathMapper, project);

                        string projectDesc = GetProjectDescription(pathMapper, revision, project, projectLogicalPath);

                            logger.WriteLine("{0}: {1} {2}", projectDesc, actionType, target.LogicalName);

                            itemInfo = pathMapper.AddItem(project, target, revisionAnalyzer.IsDestroyed(project.PhysicalName, target.PhysicalName));

                            isAddAction = ShouldLogicalPathBeIncluded(pathMapper, target.IsProject, targetLogicalPath ?? projectLogicalPath);
                        }
                        break;

                    case VssActionType.Share:
                        {
                            var shareAction = (VssShareAction)revision.Action;

                        GitActions.Commit pendingCommit = GetOrCreatePendingCommitForProject(ref pendingCommits, changeset, pathMapper, project);

                        string projectDesc = GetProjectDescription(pathMapper, revision, project, projectLogicalPath);

                            if (shareAction.Pinned)
                            {
                                logger.WriteLine("{0}: {1} {2}, Pin {3}", projectDesc, actionType, target.LogicalName, shareAction.Revision);
                                itemInfo = pathMapper.AddItem(project, target, revisionAnalyzer.IsDestroyed(project.PhysicalName, target.PhysicalName), shareAction.Revision);
                            }
                            else
                            {
                                logger.WriteLine("{0}: {1} {2}", projectDesc, actionType, target.LogicalName);
                                itemInfo = pathMapper.AddItem(project, target, revisionAnalyzer.IsDestroyed(project.PhysicalName, target.PhysicalName));
                            }

                            isAddAction = ShouldLogicalPathBeIncluded(pathMapper, target.IsProject, targetLogicalPath);
                        }
                        break;

                    case VssActionType.Recover:
                        {
                        GitActions.Commit pendingCommit = GetOrCreatePendingCommitForProject(ref pendingCommits, changeset, pathMapper, project);

                        string projectDesc = GetProjectDescription(pathMapper, revision, project, projectLogicalPath);

                            logger.WriteLine("{0}: {1} {2}", projectDesc, actionType, target.LogicalName);

                            itemInfo = pathMapper.RecoverItem(project, target, revisionAnalyzer.IsDestroyed(project.PhysicalName, target.PhysicalName));

                            isAddAction = ShouldLogicalPathBeIncluded(pathMapper, target.IsProject, targetLogicalPath);
                        }

                        break;

                    case VssActionType.Delete:
                    case VssActionType.Destroy:
                        {
                        GitActions.Commit pendingCommit = GetOrCreatePendingCommitForProject(ref pendingCommits, changeset, pathMapper, project);

                        string projectDesc = GetProjectDescription(pathMapper, revision, project, projectLogicalPath);

                            logger.WriteLine("{0}: {1} {2}", projectDesc, actionType, target.LogicalName);

                            itemInfo = pathMapper.DeleteItem(project, target, (VssActionType.Destroy == actionType));

                            if (targetLogicalPath != null && targetWorkDirPath != null)
                            {
                                if (ShouldLogicalPathBeIncluded(pathMapper, target.IsProject, targetLogicalPath))
                                {
                                string targetWorkDirPathAsString = pathMapper.WorkDirPathToString(targetWorkDirPath);

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
                                            logger.WriteLine("NOTE: Skipping move to because {0} in project {1} is destroyed", target.LogicalName, pathMapper.LogicalPathToString(projectLogicalPath));
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
                                                logger.WriteLine("NOTE: {0} contains another file named {1}; not deleting file",
                                                    projectDesc, target.LogicalName);
                                            }
                                            else
                                            {
                                            string targetLogicalPathAsString = pathMapper.LogicalPathToString(targetLogicalPath);

                                                pendingCommit.DeleteFileOrDirectory(revision, new GitActions.DeleteFile(targetWorkDirPathAsString), targetLogicalPathAsString);
                                            }
                                        }
                                        else
                                        {
                                            logger.WriteLine("NOTE: Skipping move to because {0} in project {1} is destroyed", target.LogicalName, pathMapper.LogicalPathToString(projectLogicalPath));
                                        }
                                    }
                                }
                            }
                        }
                        break;

                    case VssActionType.Rename:
                        {
                            var renameAction = (VssRenameAction)revision.Action;

                        GitActions.Commit pendingCommit = GetOrCreatePendingCommitForProject(ref pendingCommits, changeset, pathMapper, project);

                        string projectDesc = GetProjectDescription(pathMapper, revision, project, projectLogicalPath);

                            logger.WriteLine("{0}: {1} {2} to {3}",
                                projectDesc, actionType, renameAction.OriginalName, target.LogicalName);

                            itemInfo = pathMapper.RenameItem(target);

                            if (targetLogicalPath != null && targetWorkDirPath != null)
                            {
                                if (ShouldLogicalPathBeIncluded(pathMapper, target.IsProject, targetLogicalPath))
                                {
                                    var sourceLogicalPath = new List<string>(projectLogicalPath);
                                    sourceLogicalPath.Add(renameAction.OriginalName);

                                    var sourceWorkDirPath = new List<string>(projectWorkDirPath);
                                    sourceWorkDirPath.Add(renameAction.OriginalName);

                                string sourceWorkDirPathAsString = pathMapper.WorkDirPathToString(sourceWorkDirPath);
                                string targetWorkDirPathAsString = pathMapper.WorkDirPathToString(targetWorkDirPath);

                                    bool destroyed = target.IsProject ? ((VssProjectInfo)itemInfo).Destroyed : pathMapper.GetFileDestroyed(project, target.PhysicalName);

                                    if (!destroyed)
                                    {
                                        // renaming a file or a project that contains files?
                                        var projectInfo = itemInfo as VssProjectInfo;
                                        if (projectInfo == null || projectInfo.ContainsFiles())
                                        {
                                            if (!dryRun)
                                            {
                                            string sourceLogicalPathAsString = pathMapper.LogicalPathToString(sourceLogicalPath);
                                            string targetLogicalPathAsString = pathMapper.LogicalPathToString(targetLogicalPath);

                                            string message = String.Format("{0} (from {1})", targetLogicalPathAsString, sourceLogicalPathAsString);

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
                                        logger.WriteLine("NOTE: Skipping move to because {0} in project {1} is destroyed", target.LogicalName, pathMapper.LogicalPathToString(projectLogicalPath));
                                    }
                                }
                            }
                        }
                        break;

                    case VssActionType.MoveFrom:
                        // if both MoveFrom & MoveTo are present (e.g.
                        // one of them has not been destroyed), only one
                        // can succeed, so check that the source exists
                        {
                            var moveFromAction = (VssMoveFromAction)revision.Action;

                        GitActions.Commit pendingCommit = GetOrCreatePendingCommitForProject(ref pendingCommits, changeset, pathMapper, project);

                        string projectDesc = GetProjectDescription(pathMapper, revision, project, projectLogicalPath);

                        string targetLogicalPathAsString = pathMapper.LogicalPathToString(targetLogicalPath);

                            logger.WriteLine("{0}: Move from {1} to {2}",
                                projectDesc, moveFromAction.OriginalProject, targetLogicalPathAsString);

                            if (pathMapper.IsProjectRooted(moveFromAction.Name.PhysicalName))
                            {
                            List<string> sourceLogicalPath = pathMapper.GetProjectLogicalPath(moveFromAction.Name.PhysicalName);
                            List<string> sourceWorkDirPath = pathMapper.GetProjectWorkDirPath(moveFromAction.Name.PhysicalName);

                            VssProjectInfo projectInfo = pathMapper.MoveProjectFrom(project, target, moveFromAction.OriginalProject);

                                if (targetLogicalPath != null && targetWorkDirPath != null && !projectInfo.Destroyed)
                                {
                                    if (ShouldLogicalPathBeIncluded(pathMapper, target.IsProject, targetLogicalPath))
                                    {
                                    string targetWorkDirPathAsString = pathMapper.WorkDirPathToString(targetWorkDirPath);

                                        if (sourceLogicalPath != null && sourceWorkDirPath != null)
                                        {
                                        string sourceWorkDirPathAsString = pathMapper.WorkDirPathToString(sourceWorkDirPath);

                                            if (Directory.Exists(sourceWorkDirPathAsString))
                                            {
                                                if (projectInfo.ContainsFiles())
                                                {
                                                string sourceLogicalPathAsString = pathMapper.LogicalPathToString(sourceLogicalPath);

                                                string message = String.Format("{0} (from {1})", targetLogicalPathAsString, sourceLogicalPathAsString);

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
                                    logger.WriteLine("NOTE: Skipping move to because {0} is destroyed", pathMapper.LogicalPathToString(projectLogicalPath));
                                }
                            }
                            else
                            {
                                // the moved project is currently not part of project tree

                                bool destroyed = revisionAnalyzer.IsDestroyed(project.PhysicalName, target.PhysicalName);

                                if (!destroyed)
                                {
                                    logger.WriteLine("NOTE: Moving unmapped project {0} from {1} to {2}",
                                                     target.LogicalName, moveFromAction.OriginalProject, pathMapper.LogicalPathToString(projectLogicalPath));

                                    // add it again
                                    itemInfo = pathMapper.AddItem(project, target, destroyed);

                                    isAddAction = ShouldLogicalPathBeIncluded(pathMapper, target.IsProject, targetLogicalPath);
                                }
                                else
                                {
                                    logger.WriteLine("NOTE: Skipping move to because {0} in project {1} is destroyed", target.LogicalName, pathMapper.LogicalPathToString(projectLogicalPath));
                                }
                            }
                        }
                        break;

                    case VssActionType.MoveTo:
                        {
                            // handle actual moves in MoveFrom; this just does cleanup of destroyed projects
                            var moveToAction = (VssMoveToAction)revision.Action;

                        GitActions.Commit pendingCommit = GetOrCreatePendingCommitForProject(ref pendingCommits, changeset, pathMapper, project);

                        string projectDesc = GetProjectDescription(pathMapper, revision, project, projectLogicalPath);

                        string targetLogicalPathAsString = pathMapper.LogicalPathToString(targetLogicalPath);

                            logger.WriteLine("{0}: Move to {1} from {2}",
                                projectDesc, moveToAction.NewProject, targetLogicalPathAsString);

                        VssProjectInfo projectInfo = pathMapper.MoveProjectTo(project, target, moveToAction.NewProject);

                            if (!projectInfo.Destroyed && targetLogicalPath != null && targetWorkDirPath != null)
                            {
                                if (ShouldLogicalPathBeIncluded(pathMapper, target.IsProject, targetLogicalPath))
                                {
                                string targetWorkDirPathAsString = pathMapper.WorkDirPathToString(targetWorkDirPath);

                                    // project was moved to a now-destroyed project; remove empty directory
                                    pendingCommit.AddAction(revision, new GitActions.DeleteDirectory(targetWorkDirPathAsString, false), false);
                                }
                            }
                        }
                        break;

                    case VssActionType.Pin:
                        {
                        GitActions.Commit pendingCommit = GetOrCreatePendingCommitForProject(ref pendingCommits, changeset, pathMapper, project);

                        string projectDesc = GetProjectDescription(pathMapper, revision, project, projectLogicalPath);

                            bool destroyed = revisionAnalyzer.IsDestroyed(project.PhysicalName, target.PhysicalName);
                            var pinAction = (VssPinAction)revision.Action;
                            if (pinAction.Pinned)
                            {
                                logger.WriteLine("{0}: Pin {1}", projectDesc, target.LogicalName);
                                itemInfo = pathMapper.PinItem(project, target, pinAction.Revision, destroyed);
                                writeFile = !destroyed && ShouldLogicalPathBeIncluded(pathMapper, target.IsProject, targetLogicalPath);
                            }
                            else
                            {
                                logger.WriteLine("{0}: Unpin {1}", projectDesc, target.LogicalName);
                                itemInfo = pathMapper.UnpinItem(project, target, destroyed);
                                writeFile = !destroyed && ShouldLogicalPathBeIncluded(pathMapper, target.IsProject, targetLogicalPath);
                            }
                        }
                        break;

                    case VssActionType.Branch:
                        {
                        GitActions.Commit pendingCommit = GetOrCreatePendingCommitForProject(ref pendingCommits, changeset, pathMapper, project);

                        string projectDesc = GetProjectDescription(pathMapper, revision, project, projectLogicalPath);

                            var branchAction = (VssBranchAction)revision.Action;
                            logger.WriteLine("{0}: {1} {2}", projectDesc, actionType, target.LogicalName);

                            itemInfo = pathMapper.BranchFile(project, target, branchAction.Source, revisionAnalyzer.IsDestroyed(project.PhysicalName, target.PhysicalName));
                        }
                        break;

                    case VssActionType.Archive:
                        // currently ignored
                        {
                        GitActions.Commit pendingCommit = GetOrCreatePendingCommitForProject(ref pendingCommits, changeset, pathMapper, project);

                        string projectDesc = GetProjectDescription(pathMapper, revision, project, projectLogicalPath);

                            var archiveAction = (VssArchiveAction)revision.Action;
                            logger.WriteLine("{0}: Archive {1} to {2} (ignored)",
                                projectDesc, target.LogicalName, archiveAction.ArchivePath);
                        }
                        break;

                    case VssActionType.Restore:
                        {
                        GitActions.Commit pendingCommit = GetOrCreatePendingCommitForProject(ref pendingCommits, changeset, pathMapper, project);

                        string projectDesc = GetProjectDescription(pathMapper, revision, project, projectLogicalPath);

                            var restoreAction = (VssRestoreAction)revision.Action;
                            logger.WriteLine("{0}: Restore {1} from archive {2}",
                                projectDesc, target.LogicalName, restoreAction.ArchivePath);

                            itemInfo = pathMapper.AddItem(project, target, revisionAnalyzer.IsDestroyed(project.PhysicalName, target.PhysicalName));
                            isAddAction =  ShouldLogicalPathBeIncluded(pathMapper, target.IsProject, targetLogicalPath);
                        }
                        break;
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
                                    string targetWorkDirPathAsString = pathMapper.WorkDirPathToString(targetWorkDirPath);
                                    pendingCommit.AddAction(revision, new GitActions.CreateDirectory(targetWorkDirPathAsString), false);
                                    writeProject = true;
                                }
                                else
                                {
                                    string targetLogicalPathAsString = pathMapper.WorkDirPathToString(targetLogicalPath);
                                    logger.WriteLine("NOTE: Skipping destroyed project: {0}", targetLogicalPathAsString);
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

                            if (dryRun)
                            {
                                logger.WriteLine("{0}: Creating subdirectory {1}", projectDesc, projectInfo.LogicalName);
                            }

                            string targetWorkDirPathAsString = pathMapper.WorkDirPathToString(projectInfo.GetWorkDirPath());

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

                                string targetWorkDirPathAsString = pathMapper.WorkDirPathToString(p.Item2);

                                if (dryRun)
                                {
                                    logger.WriteLine("{0}: {1} revision {2}", targetWorkDirPathAsString, actionType, fileInfo.Version);
                                }

                                pendingCommit.AddFile(revision, new GitActions.WriteFile(database, fileInfo.PhysicalName, fileInfo.Version, targetWorkDirPathAsString), pathMapper.LogicalPathToString(p.Item1));
                            }
                        }
                    }
                    else if (writeFile)
                    {
                        if (ShouldLogicalPathBeIncluded(pathMapper, false, targetLogicalPath))
                        {
                            bool destroyed = pathMapper.GetFileDestroyed(project, target.PhysicalName);

                            GitActions.Commit pendingCommit = GetOrCreatePendingCommitForLogicalPath(ref pendingCommits, changeset, targetLogicalPath);

                            string targetLogicalPathAsString = pathMapper.LogicalPathToString(targetLogicalPath);

                            if (!destroyed)
                            {
                                // write current rev to working path
                                int version = pathMapper.GetFileVersion(project, target.PhysicalName);

                                string targetWorkDirPathAsString = pathMapper.WorkDirPathToString(targetWorkDirPath);

                                if (dryRun)
                                {
                                    logger.WriteLine("{0}: {1} revision {2}", targetWorkDirPathAsString, actionType, version);
                                }

                                pendingCommit.AddFile(revision, new GitActions.WriteFile(database, target.PhysicalName, version, targetWorkDirPathAsString), targetLogicalPathAsString);
                            }
                            else
                            {
                                logger.WriteLine("NOTE: Skipping destroyed file: {0}", targetLogicalPathAsString);
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

                    string targetWorkDirPathAsString = pathMapper.WorkDirPathToString(p.Item2);

                    if (dryRun)
                    {
                        logger.WriteLine("{0}: {1} revision {2}", targetWorkDirPathAsString, actionType, revision.Version);
                    }

                    pendingCommit.WriteFile(revision, new GitActions.WriteFile(database, target.PhysicalName, revision.Version, targetWorkDirPathAsString));
                }
            }
        }

        private bool ShouldLogicalPathBeIncluded(VssPathMapper pathMapper, bool isProject, List<string> logicalPath)
        {
            bool shouldBeIncluded = false;

            if (null != logicalPath)
            {
                string path = pathMapper.LogicalPathToString(logicalPath);

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
                        if (dryRun)
                        {
                            logger.WriteLine("Excluding project {0}", path);
                        }
                        excludedProjects.Add(path);
                    }
                    else
                    {
                        if (dryRun)
                        {
                            logger.WriteLine("Excluding file {0}", path);
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

        private string GetProjectDescription(VssPathMapper pathMapper, Revision revision, VssItemName project, List<string> projectPath)
        {
            string projectDesc = "";

            if (null != projectPath)
            {
                projectDesc = pathMapper.LogicalPathToString(projectPath);
            }
            else
            {
                projectDesc = revision.Item.ToString();

                logger.WriteLine("NOTE: {0} is currently unmapped", project.LogicalName);
            }

            return projectDesc;
        }

        private GitActions.Commit GetOrCreatePendingCommitForLogicalPath(ref Dictionary<string, GitActions.Commit> pendingCommits, Changeset changeset, List<string> logicalPath)
        {
            GitActions.Commit pendingCommit = null;

            const string pendingBranch = "master";

            if (!pendingCommits.TryGetValue(pendingBranch, out pendingCommit))
            {
                pendingCommit = CreateGitCommitAction(changeset);
                pendingCommits[pendingBranch] = pendingCommit;
            }

            return pendingCommit;
        }

        private GitActions.Commit GetOrCreatePendingCommitForProject(ref Dictionary<string, GitActions.Commit> pendingCommits, Changeset changeset, VssPathMapper pathMapper, VssItemName project)
        {
            return GetOrCreatePendingCommitForLogicalPath(ref pendingCommits, changeset, pathMapper.GetProjectLogicalPath(project.PhysicalName));
        }

        private static string GetPendingBranchFromLogicalProjectPath(List<string> logicalProjectPath)
        {
            string pendingBranch = "";

            if (null != logicalProjectPath && 2 <= logicalProjectPath.Count && VssDatabase.RootProjectName == logicalProjectPath[0])
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
            if (String.IsNullOrEmpty(tagMessage) && forceAnnotatedTags)
            {
                // use the original VSS label as the tag message if none was provided
                tagMessage = labelName;
            }

            return new GitActions.CreateTag(tagName, GetUser(user), GetEmail(user), tagMessage, localTime.ConvertAmbiguousTimeToUtc(logger));
        }

        private bool RetryCancel(ThreadStart work)
        {
            return AbortRetryIgnore(work, MessageBoxButtons.RetryCancel);
        }

        private bool AbortRetryIgnore(ThreadStart work)
        {
            return AbortRetryIgnore(work, MessageBoxButtons.AbortRetryIgnore);
        }

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

                    if (ignoreErrors)
                    {
                        retry = false;
                        continue;
                    }

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
            if (String.IsNullOrEmpty(input))
                throw new ArgumentException($"{nameof(input)} cannot be empty", nameof(input));
            return input.First().ToString().ToUpper() + input.Substring(1);
        }

        private string GetUser(string user)
        {
            if (null != userToEmailDictionary)
            {
                string email = userToEmailDictionary.GetValue(user, null);

                if (null != email)
                {
                    string[] emailParts = email.Split(new char[] { '@' }, 2, 0);

                    if (2 <= emailParts.Length)
                    {
                        string[] userNameParts = emailParts[0].Split(new char[] { '.' }, StringSplitOptions.RemoveEmptyEntries);

                        user = string.Join(" ", userNameParts.Select(s => FirstCharToUpper(s)).ToArray());
                    }
                }
            }

            return user;
        }

        private string GetEmail(string user)
        {
            string email = user.ToLower().Replace(' ', '.') + "@" + emailDomain;

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
            string baseTag = Regex.Replace(label, "[^A-Za-z0-9_-]+", "_");

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
    }
}
