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
using System.Globalization;
using System.IO;
using System.Threading;

namespace Hpdi.Vss2Git
{
    /// <summary>
    /// Wraps execution of LibGit2Sharp and implements the common LibGit2Sharp commands.
    /// </summary>
    /// <author>Trevor Robinson</author>
    class LibGit2SharpWrapper : AbstractGitWrapper
    {
        LibGit2Sharp.Repository repo = null;

        public LibGit2SharpWrapper(string repoPath, Logger logger)
            : base(repoPath, logger)
        {
        }

        protected static void DeleteDirectory(string path)
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

            repo = new LibGit2Sharp.Repository(LibGit2Sharp.Repository.Init(GetRepoPath()));
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
            LibGit2Sharp.Commands.Stage(repo, path);

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
            LibGit2Sharp.Commands.Stage(repo, paths);

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
            LibGit2Sharp.Commands.Stage(repo, "*");

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

            List<string> sourceFiles = new List<string>();
            List<string> destFiles = new List<string>();

            foreach (var file in Directory.GetFiles(sourcePath, "*", SearchOption.AllDirectories))
            {
                sourceFiles.Add(file);

                var destFile = file.Replace(sourcePath, destPath);

                destFiles.Add(destFile);

                Directory.CreateDirectory(Path.GetDirectoryName(destFile));

                needsCommit |= true;
            }

            LibGit2Sharp.Commands.Move(repo, sourceFiles, destFiles);

            try
            {
                //Delete all child Directories if they are empty
                if (Directory.Exists(sourcePath))
                {
                    string[] files = null;

                    foreach (var subdirectory in Directory.GetDirectories(sourcePath))
                    {
                        files = Directory.GetFiles(subdirectory, "*.*");

                        if (files.Length == 0)
                        {
                            Directory.Delete(subdirectory);
                        }
                    }

                    files = Directory.GetFiles(sourcePath);

                    if (files.Length == 0)
                    {
                        Directory.Delete(sourcePath);
                    }
                }
            }
            catch (IOException e)
            {
                this.Logger.WriteLine("Deleting of empty directories failed: {0}", e.Message);
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
                throw new ArgumentException(String.Format("Specified time {0} is not Utc", utcTime), "utcTime");
            }

            // Create the committer's signature and commit
            var author = new LibGit2Sharp.Signature(authorName, authorEmail, utcTime);
            var committer = author;

            // Commit to the repository
            repo.Commit(comment, author, committer, default(LibGit2Sharp.CommitOptions));

            return true;
        }

        public override void Tag(string name, string taggerName, string taggerEmail, string comment, DateTime utcTime)
        {
            if (utcTime.Kind != DateTimeKind.Utc)
            {
                throw new ArgumentException(String.Format("Specified time {0} is not Utc", utcTime), "utcTime");
            }

            var commiter = new LibGit2Sharp.Signature(taggerName, taggerEmail, utcTime);
            repo.Tags.Add(name, RetrieveHeadCommit(repo), commiter, comment);
        }

        private static LibGit2Sharp.Commit RetrieveHeadCommit(LibGit2Sharp.IRepository repository)
        {
            LibGit2Sharp.Commit commit = repository.Head.Tip;

            GitObjectIsNotNull(commit, "HEAD");

            return commit;
        }

        private static void GitObjectIsNotNull(LibGit2Sharp.GitObject gitObject, string identifier)
        {
            if (gitObject != null)
            {
                return;
            }

            var messageFormat = "No valid git object identified by '{0}' exists in the repository.";

            if (string.Equals("HEAD", identifier, StringComparison.Ordinal))
            {
                throw new LibGit2Sharp.UnbornBranchException(messageFormat, identifier);
            }

            throw new LibGit2Sharp.NotFoundException(messageFormat, identifier);
        }
       
    }
}
