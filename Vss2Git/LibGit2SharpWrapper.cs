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

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace Hpdi.Vss2Git
{
    /// <summary>
    /// Wraps execution of LibGit2Sharp and implements the common LibGit2Sharp commands.
    /// </summary>
    /// <author>Dariusz Bywalec</author>
    sealed class LibGit2SharpWrapper : AbstractGitWrapper
    {
        LibGit2Sharp.Repository repo = null;
        LibGit2Sharp.StageOptions stageOptions = null;

        public LibGit2SharpWrapper(string repoPath, Logger logger)
            : base(repoPath, logger)
        {
        }

        /*protected*/ static void DeleteDirectory(string path)
        {
            // this method should be used with caution - therefore it is protected
            if (!Directory.Exists(path))
            {
                return;
            }
            string[] files = Directory.GetFiles(path);
            string[] dirs = Directory.GetDirectories(path);

            foreach (string file in files)
            {
                File.SetAttributes(file, FileAttributes.Normal);
                File.Delete(file);
            }

            foreach (string dir in dirs)
            {
                DeleteDirectory(dir);
            }

            try
            {
                Directory.Delete(path, false);
            }
            catch (IOException)
            {
                Thread.Sleep(0);
                Directory.Delete(path, false);
            }
        }

        public override bool FindExecutable()
        {
            return true;
        }

        public override void Init(bool resetRepo)
        {
            if (resetRepo)
            {
                DeleteDirectory(GetRepoPath());
                Thread.Sleep(0);
                Directory.CreateDirectory(GetRepoPath());
            }

            if (base.IncludeIgnoredFiles)
            {
                stageOptions = new LibGit2Sharp.StageOptions()
                {
                    IncludeIgnored = true,
                };
            }

            string repoPath = LibGit2Sharp.Repository.Init(GetRepoPath());
            repo = new LibGit2Sharp.Repository(repoPath);
        }

        public override void Exit()
        {
            if (null != repo)
            {
                repo.Dispose();
                repo = null;
            }
        }

        public override void Configure()
        {
            if (CommitEncoding.WebName != "utf-8")
            {
                repo.Config.Set<string>("i18n.commitencoding", CommitEncoding.WebName);
            }

            if (!LibGit2Sharp.Repository.IsValid(GetRepoPath()))
            {
                throw new ApplicationException("The Git repository is not valid");
            }
        }

        public override bool Add(string path)
        {
            // Stage the file
            LibGit2Sharp.Commands.Stage(repo, path, stageOptions);

            SetNeedsCommit();

            return true;
        }

        public override bool Add(IEnumerable<string> paths)
        {
            if (CollectionUtil.IsEmpty(paths))
            {
                return false;
            }

            // Stage the files
            LibGit2Sharp.Commands.Stage(repo, paths, stageOptions);

            SetNeedsCommit();

            return true;
        }

        public override bool AddDir(string path)
        {
            // do nothing - git does not care about directories
            return true;
        }

        public override bool AddAll()
        {
            LibGit2Sharp.Commands.Stage(repo, "*", stageOptions);

            SetNeedsCommit();

            return true;
        }

        public override void RemoveFile(string path)
        {
            // Removes the file
            LibGit2Sharp.Commands.Remove(repo, path, true);

            SetNeedsCommit();
        }

        public override void RemoveDir(string path, bool recursive)
        {
            // Removes the directory
            LibGit2Sharp.Commands.Remove(repo, path, true);

            SetNeedsCommit();
        }

        public override void RemoveEmptyDir(string path)
        {
            // do nothing - remove only on file system - git doesn't care about directories with no files
        }

        public override void MoveFile(string sourcePath, string destPath)
        {
            LibGit2Sharp.Commands.Move(repo, sourcePath, destPath);

            SetNeedsCommit();
        }

        public override void MoveDir(string sourcePath, string destPath)
        {
            bool needsCommit = false;

            List<string> sourceFiles = [];
            List<string> destFiles = [];

            foreach (string file in Directory.GetFiles(sourcePath, "*", SearchOption.AllDirectories))
            {
                sourceFiles.Add(file);

                string destFile = file.Replace(sourcePath, destPath);

                destFiles.Add(destFile);

                Directory.CreateDirectory(Path.GetDirectoryName(destFile));

                needsCommit |= true;
            }

            if (sourceFiles.Count > 0 && destFiles.Count > 0)
            {
                LibGit2Sharp.Commands.Move(repo, sourceFiles, destFiles);
            }
            else if (sourceFiles.Count == 0 && destFiles.Count == 0)
            {
                MoveEmptyDir(sourcePath, destPath);
            }
            else
            {
                throw new InvalidOperationException($"MOVEDIRWTF: SrcCount={sourceFiles.Count} DstCount={destFiles.Count}");
            }

            try
            {
                //Delete all child Directories if they are empty
                if (Directory.Exists(sourcePath))
                {
                    string[] files = null;

                    foreach (string subdirectory in Directory.GetDirectories(sourcePath))
                    {
                        files = Directory.GetFiles(subdirectory, "*.*");

                        if (files.Length == 0)
                        {
                            // git doesn't care about directories with no files
                            Directory.Delete(subdirectory, true);
                        }
                    }

                    files = Directory.GetFiles(sourcePath);

                    if (files.Length == 0)
                    {
                        // git doesn't care about directories with no files
                        Directory.Delete(sourcePath, true);
                    }
                }
            }
            catch (IOException e)
            {
                this.Logger.WriteLine($"Deleting of empty directories failed: {e.Message}");
            }

            if (needsCommit)
            {
                SetNeedsCommit();
            }
        }

        public override void MoveEmptyDir(string sourcePath, string destPath)
        {
            // move only on file system - git doesn't care about directories with no files
            Directory.Move(sourcePath, destPath);
        }

        public override bool DoCommit(string authorName, string authorEmail, string comment, DateTime utcTime)
        {
            if (utcTime.Kind != DateTimeKind.Utc)
            {
                throw new ArgumentException($"Specified time {utcTime} is not Utc", nameof(utcTime));
            }

            // Create the committer's signature and commit
            var author = new LibGit2Sharp.Signature(authorName, authorEmail, utcTime);
            LibGit2Sharp.Signature committer = author;

            // Commit to the repository
            repo.Commit(comment, author, committer, default);

            return true;
        }

        public override void Tag(string name, string taggerName, string taggerEmail, string comment, DateTime utcTime)
        {
            if (utcTime.Kind != DateTimeKind.Utc)
            {
                throw new ArgumentException($"Specified time {utcTime} is not Utc", nameof(utcTime));
            }

            var commiter = new LibGit2Sharp.Signature(taggerName, taggerEmail, utcTime);
            LibGit2Sharp.Commit commit = RetrieveHeadCommit(repo);
            repo.Tags.Add(name, commit, commiter, comment);
        }

        private static LibGit2Sharp.Commit RetrieveHeadCommit(LibGit2Sharp.IRepository repository)
        {
            LibGit2Sharp.Branch head = repository.Head;
            LibGit2Sharp.Commit commit = head.Tip;

            GitObjectIsNotNull(commit, "HEAD");

            return commit;
        }

        private static void GitObjectIsNotNull(LibGit2Sharp.GitObject gitObject, string identifier)
        {
            if (gitObject != null)
            {
                return;
            }

            const string messageFormat = "No valid git object identified by '{0}' exists in the repository.";

            if (string.Equals("HEAD", identifier, StringComparison.Ordinal))
            {
                throw new LibGit2Sharp.UnbornBranchException(messageFormat, identifier);
            }

            throw new LibGit2Sharp.NotFoundException(messageFormat, identifier);
        }

    }
}
