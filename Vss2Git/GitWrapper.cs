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
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;

namespace Hpdi.Vss2Git
{
    /// <summary>
    /// Wraps execution of Git and implements the common Git commands.
    /// </summary>
    /// <author>Trevor Robinson</author>
    class GitWrapper : AbstractGitWrapper
    {
        private string gitExecutable = "git.exe";
        private string gitInitialArguments = null;

        public string GitExecutable
        {
            get { return gitExecutable; }
            set { gitExecutable = value; }
        }

        public string GitInitialArguments
        {
            get { return gitInitialArguments; }
            set { gitInitialArguments = value; }
        }

        public GitWrapper(string repoPath, Logger logger)
            : base(repoPath, logger)
        {
        }


        private void SetConfig(string name, string value)
        {
            GitExec("config " + name + " " + Quote(value));
        }

        class TempFile : IDisposable
        {
            private readonly string name;
            private readonly FileStream fileStream;

            public string Name
            {
                get { return name; }
            }

            public TempFile()
            {
                name = Path.GetTempFileName();
                fileStream = new FileStream(name, FileMode.Truncate, FileAccess.Write, FileShare.Read);
            }

            public void Write(string text, Encoding encoding)
            {
                var bytes = encoding.GetBytes(text);
                fileStream.Write(bytes, 0, bytes.Length);
                fileStream.Flush();
            }

            public void Dispose()
            {
                if (fileStream != null)
                {
                    fileStream.Dispose();
                }
                if (name != null)
                {
                    File.Delete(name);
                }
            }
        }

        private void AddComment(string comment, ref string args, out TempFile tempFile)
        {
            tempFile = null;
            if (!string.IsNullOrEmpty(comment))
            {
                // need to use a temporary file to specify the comment when not
                // using the system default code page or it contains newlines
                if (this.CommitEncoding.CodePage != Encoding.Default.CodePage || comment.IndexOf('\n') >= 0)
                {
                    this.Logger.WriteLine("Generating temp file for comment: {0}", comment);
                    tempFile = new TempFile();
                    tempFile.Write(comment, this.CommitEncoding);

                    // temporary path might contain spaces (e.g. "Documents and Settings")
                    args += " -F " + Quote(tempFile.Name);
                }
                else
                {
                    args += " -m " + Quote(comment);
                }
            }
        }

        private void GitExec(string args)
        {
            var startInfo = GetStartInfo(args);
            ExecuteUnless(startInfo, null);
        }

        private ProcessStartInfo GetStartInfo(string args)
        {
            if (!string.IsNullOrEmpty(gitInitialArguments))
            {
                args = gitInitialArguments + " " + args;
            }

            var startInfo = new ProcessStartInfo(gitExecutable, args);
            startInfo.UseShellExecute = false;
            startInfo.RedirectStandardInput = true;
            startInfo.RedirectStandardOutput = true;
            startInfo.RedirectStandardError = true;
            startInfo.WorkingDirectory = GetRepoPath();
            startInfo.CreateNoWindow = true;
            return startInfo;
        }

        private bool ExecuteUnless(ProcessStartInfo startInfo, string unless)
        {
            string stdout, stderr;
            int exitCode = Execute(startInfo, out stdout, out stderr);
            if (exitCode != 0)
            {
                if (string.IsNullOrEmpty(unless) ||
                    ((string.IsNullOrEmpty(stdout) || !stdout.Contains(unless)) &&
                     (string.IsNullOrEmpty(stderr) || !stderr.Contains(unless))))
                {
                    FailExitCode(startInfo.FileName, startInfo.Arguments, stdout, stderr, exitCode);
                }
            }
            return exitCode == 0;
        }

        private static void FailExitCode(string exec, string args, string stdout, string stderr, int exitCode)
        {
            throw new ProcessExitException(
                string.Format("git returned exit code {0}", exitCode),
                exec, args, stdout, stderr);
        }

        private int Execute(ProcessStartInfo startInfo, out string stdout, out string stderr)
        {
            this.Logger.WriteLine("Executing: {0} {1}", startInfo.FileName, startInfo.Arguments);
            this.Stopwatch.Start();
            try
            {
                using (var process = Process.Start(startInfo))
                {
                    process.StandardInput.Close();
                    var stdoutReader = new AsyncLineReader(process.StandardOutput.BaseStream);
                    var stderrReader = new AsyncLineReader(process.StandardError.BaseStream);

                    var activityEvent = new ManualResetEvent(false);
                    EventHandler activityHandler = delegate { activityEvent.Set(); };
                    process.Exited += activityHandler;
                    stdoutReader.DataReceived += activityHandler;
                    stderrReader.DataReceived += activityHandler;

                    var stdoutBuffer = new StringBuilder();
                    var stderrBuffer = new StringBuilder();
                    while (true)
                    {
                        activityEvent.Reset();

                        while (true)
                        {
                            string line = stdoutReader.ReadLine();
                            if (line != null)
                            {
                                line = line.TrimEnd();
                                if (stdoutBuffer.Length > 0)
                                {
                                    stdoutBuffer.AppendLine();
                                }
                                stdoutBuffer.Append(line);
                                this.Logger.Write('>');
                            }
                            else
                            {
                                line = stderrReader.ReadLine();
                                if (line != null)
                                {
                                    line = line.TrimEnd();
                                    if (stderrBuffer.Length > 0)
                                    {
                                        stderrBuffer.AppendLine();
                                    }
                                    stderrBuffer.Append(line);
                                    this.Logger.Write('!');
                                }
                                else
                                {
                                    break;
                                }
                            }
                            this.Logger.WriteLine(line);
                        }

                        if (process.HasExited)
                        {
                            break;
                        }

                        activityEvent.WaitOne(1000);
                    }

                    stdout = stdoutBuffer.ToString();
                    stderr = stderrBuffer.ToString();
                    return process.ExitCode;
                }
            }
            catch (FileNotFoundException e)
            {
                throw new ProcessException("Executable not found.",
                    e, startInfo.FileName, startInfo.Arguments);
            }
            catch (Win32Exception e)
            {
                throw new ProcessException("Error executing external process.",
                    e, startInfo.FileName, startInfo.Arguments);
            }
            finally
            {
                this.Stopwatch.Stop();
            }
        }

        private bool FindInPathVar(string filename, out string foundPath)
        {
            string path = Environment.GetEnvironmentVariable("PATH");
            if (!string.IsNullOrEmpty(path))
            {
                return FindInPaths(filename, path.Split(Path.PathSeparator), out foundPath);
            }
            foundPath = null;
            return false;
        }

        private bool FindInPaths(string filename, IEnumerable<string> searchPaths, out string foundPath)
        {
            foreach (string searchPath in searchPaths)
            {
                string path = Path.Combine(searchPath, filename);
                if (File.Exists(path))
                {
                    foundPath = path;
                    return true;
                }
            }
            foundPath = null;
            return false;
        }

        protected virtual void ValidateRepoPath()
        {
            const string metaDir = ".git";
            string[] files = Directory.GetFiles(GetRepoPath());
            string[] dirs = Directory.GetDirectories(GetRepoPath());
            if (files.Length > 0)
            {
                throw new ApplicationException("The output directory is not empty");
            }
            string metaDirSuffix = "\\" + metaDir;
            foreach (string dir in dirs)
            {
                if (!dir.EndsWith(metaDirSuffix))
                {
                    throw new ApplicationException("The output directory is not empty");
                }
            }
            if (!Directory.Exists(Path.Combine(GetRepoPath(), metaDir)))
            {
                throw new ApplicationException("The output directory does not contain the meta directory " + metaDir);
            }
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
            string foundPath;
            if (FindInPathVar("git.exe", out foundPath))
            {
                gitExecutable = foundPath;
                gitInitialArguments = null;
                this.ShellQuoting = false;
                return true;
            }
            if (FindInPathVar("git.cmd", out foundPath))
            {
                gitExecutable = "cmd.exe";
                gitInitialArguments = "/c git";
                this.ShellQuoting = true;
                return true;
            }
            return false;
        }

        public override void Init(bool resetRepo)
        {
            if (resetRepo)
            {
                DeleteDirectory(GetRepoPath());
                Thread.Sleep(0);
                Directory.CreateDirectory(GetRepoPath());
            }
            GitExec("init");
        }

        public override void Exit()
        {
        }

        public override void Configure()
        {
            if (CommitEncoding.WebName != "utf-8")
            {
                SetConfig("i18n.commitencoding", CommitEncoding.WebName);
            }
            ValidateRepoPath();
        }

        public override bool Add(string path)
        {
            var startInfo = GetStartInfo("add -- " + QuoteRelativePath(path));

            // add fails if there are no files (directories don't count)
            bool result = ExecuteUnless(startInfo, "did not match any files");

            if (result)
            {
                SetNeedsCommit();
            }

            return result;
        }

        public override bool Add(IEnumerable<string> paths)
        {
            if (CollectionUtil.IsEmpty(paths))
            {
                return false;
            }

            var args = new StringBuilder("add -- ");
            CollectionUtil.Join(args, " ", CollectionUtil.Transform<string, string>(paths, QuoteRelativePath));
            var startInfo = GetStartInfo(args.ToString());

            // add fails if there are no files (directories don't count)
            bool result = ExecuteUnless(startInfo, "did not match any files");

            if (result)
            {
                SetNeedsCommit();
            }

            return result;
        }

        public override bool AddDir(string path)
        {
            // do nothing - git does not care about directories
            return true;
        }

        public override bool AddAll()
        {
            var startInfo = GetStartInfo("add -A");

            // add fails if there are no files (directories don't count)
            bool result = ExecuteUnless(startInfo, "did not match any files");
            if (result) SetNeedsCommit();
            return result;
        }

        public override void RemoveFile(string path)
        {
            GitExec("rm -- " + QuoteRelativePath(path));
            SetNeedsCommit();
        }

        public override void RemoveDir(string path, bool recursive)
        {
            GitExec("rm " + (recursive ? "-r -f " : "") + "-- " + QuoteRelativePath(path));
            SetNeedsCommit();
        }

        public override void RemoveEmptyDir(string path)
        {
            // do nothing - remove only on file system - git doesn't care about directories with no files
        }

        public override void Move(string sourcePath, string destPath)
        {
            GitExec("mv -- " + QuoteRelativePath(sourcePath) + " " + QuoteRelativePath(destPath));
            SetNeedsCommit();
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

            TempFile commentFile;

            var args = "commit";
            AddComment(comment, ref args, out commentFile);

            using (commentFile)
            {
                var startInfo = GetStartInfo(args);
                startInfo.EnvironmentVariables["GIT_AUTHOR_NAME"] = authorName;
                startInfo.EnvironmentVariables["GIT_AUTHOR_EMAIL"] = authorEmail;
                startInfo.EnvironmentVariables["GIT_AUTHOR_DATE"] = GetUtcTimeString(utcTime);

                // also setting the committer is supposedly useful for converting to Mercurial
                startInfo.EnvironmentVariables["GIT_COMMITTER_NAME"] = authorName;
                startInfo.EnvironmentVariables["GIT_COMMITTER_EMAIL"] = authorEmail;
                startInfo.EnvironmentVariables["GIT_COMMITTER_DATE"] = GetUtcTimeString(utcTime);

                // ignore empty commits, since they are non-trivial to detect
                // (e.g. when renaming a directory)
                return ExecuteUnless(startInfo, "nothing to commit");
            }
        }

        public override void Tag(string name, string taggerName, string taggerEmail, string comment, DateTime utcTime)
        {
            if (utcTime.Kind != DateTimeKind.Utc)
            {
                throw new ArgumentException(String.Format("Specified time {0} is not Utc", utcTime), "utcTime");
            }

            TempFile commentFile;

            var args = "tag";
            AddComment(comment, ref args, out commentFile);

            // tag names are not quoted because they cannot contain whitespace or quotes
            args += " -- " + name;

            using (commentFile)
            {
                var startInfo = GetStartInfo(args);
                startInfo.EnvironmentVariables["GIT_COMMITTER_NAME"] = taggerName;
                startInfo.EnvironmentVariables["GIT_COMMITTER_EMAIL"] = taggerEmail;
                startInfo.EnvironmentVariables["GIT_COMMITTER_DATE"] = GetUtcTimeString(utcTime);

                ExecuteUnless(startInfo, null);
            }
        }
    }
}
