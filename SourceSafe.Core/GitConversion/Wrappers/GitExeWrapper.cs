using System.ComponentModel;
using System.Diagnostics;
using System.Text;

namespace SourceSafe.GitConversion.Wrappers
{
    /// <summary>
    /// Wraps execution of Git and implements the common Git commands.
    /// </summary>
    public sealed partial class GitExeWrapper : AbstractGitWrapper
    {
        public string GitExecutable { get; set; } = "git.exe";

        public string? GitInitialArguments { get; set; } = null;

        public GitExeWrapper(string repoPath, SourceSafe.IO.SimpleLogger logger)
            : base(repoPath, logger)
        {
        }

        private void SetConfig(
            string name,
            string value)
        {
            GitExec($"config {name} {Quote(value)}");
        }

        private void AddComment(
            string comment,
            ref string args,
            out TempFile? tempFile)
        {
            tempFile = null;
            if (!string.IsNullOrEmpty(comment))
            {
                // need to use a temporary file to specify the comment when not
                // using the system default code page or it contains newlines
                if (CommitEncoding.CodePage != Encoding.Default.CodePage || comment.Contains('\n'))
                {
                    Logger.WriteLine("Generating temp file for comment: {0}", comment);
                    tempFile = new TempFile();
                    tempFile.Write(comment, CommitEncoding);

                    // temporary path might contain spaces (e.g. "Documents and Settings")
                    args += " -F " + Quote(tempFile.Name);
                }
                else
                {
                    args += " -m " + Quote(comment);
                }
            }
        }

        private void GitExec(
            string args)
        {
            ProcessStartInfo startInfo = GetStartInfo(args);
            ExecuteUnless(startInfo, null);
        }

        private ProcessStartInfo GetStartInfo(
            string args)
        {
            if (!string.IsNullOrEmpty(GitInitialArguments))
            {
                args = GitInitialArguments + " " + args;
            }

            var startInfo = new ProcessStartInfo(GitExecutable, args)
            {
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                WorkingDirectory = GetRepoPath(),
                CreateNoWindow = true
            };
            return startInfo;
        }

        private bool ExecuteUnless(
            ProcessStartInfo startInfo,
            string? unless)
        {
            int exitCode = Execute(startInfo, out string stdout, out string stderr);
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

        private static void FailExitCode(
            string exec,
            string args,
            string stdout,
            string stderr,
            int exitCode)
        {
            throw new Exceptions.ExternalProcessExitException(
                $"git returned exit code {exitCode}",
                exec, args, stdout, stderr);
        }

        private int Execute(ProcessStartInfo startInfo, out string stdout, out string stderr)
        {
            Logger.WriteLine($"Executing: {startInfo.FileName} {startInfo.Arguments}");
            Stopwatch.Start();
            try
            {
                using (Process process = Process.Start(startInfo)!)
                {
                    process.StandardInput.Close();
                    var stdoutReader = new SourceSafe.IO.AsyncLineReader(process.StandardOutput.BaseStream);
                    var stderrReader = new SourceSafe.IO.AsyncLineReader(process.StandardError.BaseStream);

                    var activityEvent = new ManualResetEvent(false);
                    void ActivityHandlerLambda(object? sender, EventArgs e) { activityEvent.Set(); }
                    process.Exited += ActivityHandlerLambda;
                    stdoutReader.DataReceived += ActivityHandlerLambda;
                    stderrReader.DataReceived += ActivityHandlerLambda;

                    var stdoutBuffer = new StringBuilder();
                    var stderrBuffer = new StringBuilder();
                    while (true)
                    {
                        activityEvent.Reset();

                        while (true)
                        {
                            string? line = stdoutReader.ReadLine();
                            if (line != null)
                            {
                                line = line.TrimEnd();
                                if (stdoutBuffer.Length > 0)
                                {
                                    stdoutBuffer.AppendLine();
                                }
                                stdoutBuffer.Append(line);
                                Logger.Write('>');
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
                                    Logger.Write('!');
                                }
                                else
                                {
                                    break;
                                }
                            }
                            Logger.WriteLine(line);
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
                throw new Exceptions.ExternalProcessException("Executable not found.",
                    e, startInfo.FileName, startInfo.Arguments);
            }
            catch (Win32Exception e)
            {
                throw new Exceptions.ExternalProcessException("Error executing external process.",
                    e, startInfo.FileName, startInfo.Arguments);
            }
            finally
            {
                Stopwatch.Stop();
            }
        }

        private static bool FindInPathVar(
            string filename,
            out string? foundPath)
        {
            string? path = Environment.GetEnvironmentVariable("PATH");
            if (!string.IsNullOrEmpty(path))
            {
                return FindInPaths(filename, path.Split(Path.PathSeparator), out foundPath);
            }
            foundPath = null;
            return false;
        }

        private static bool FindInPaths(
            string filename,
            IEnumerable<string> searchPaths,
            out string? foundPath)
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

        /*protected virtual*/
        void ValidateRepoPath()
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
                throw new ApplicationException($"The output directory does not contain the meta directory {metaDir}");
            }
        }

        /*protected*/
        static void DeleteDirectory(string path)
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
            if (FindInPathVar("git.exe", out string? foundPath))
            {
                GitExecutable = foundPath!;
                GitInitialArguments = null;
                ShellQuoting = false;
                return true;
            }
            if (FindInPathVar("git.cmd", out _))
            {
                GitExecutable = "cmd.exe";
                GitInitialArguments = "/c git";
                ShellQuoting = true;
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
            ProcessStartInfo startInfo = GetStartInfo($"add -- {QuoteRelativePath(path)}");

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
            if (paths.IsNullOrEmpty())
            {
                return false;
            }

            var args = new StringBuilder("add -- ");
            CollectionUtils.Join(args, " ", CollectionUtils.Transform(paths, QuoteRelativePath));
            ProcessStartInfo startInfo = GetStartInfo(args.ToString());

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
            ProcessStartInfo startInfo = GetStartInfo("add -A");

            // add fails if there are no files (directories don't count)
            bool result = ExecuteUnless(startInfo, "did not match any files");
            if (result)
            {
                SetNeedsCommit();
            }
            return result;
        }

        public override void RemoveFile(string path)
        {
            GitExec("rm -- " + QuoteRelativePath(path));
            SetNeedsCommit();
        }

        public override void RemoveDir(string path, bool recursive)
        {
            GitExec($"rm {(recursive ? "-r -f " : "")}-- {QuoteRelativePath(path)}");
            SetNeedsCommit();
        }

        public override void RemoveEmptyDir(string path)
        {
            // do nothing - remove only on file system - git doesn't care about directories with no files
        }

        public override void MoveFile(
            string sourcePath,
            string destinationPath)
        {
            GitExec($"mv -- {QuoteRelativePath(sourcePath)} {QuoteRelativePath(destinationPath)}");
            SetNeedsCommit();
        }

        public override void MoveDir(
            string sourcePath,
            string destinationPath)
        {
            GitExec($"mv -- {QuoteRelativePath(sourcePath)} {QuoteRelativePath(destinationPath)}");
            SetNeedsCommit();
        }

        public override void MoveEmptyDir(
            string sourcePath,
            string destinationPath)
        {
            // move only on file system - git doesn't care about directories with no files
            Directory.Move(sourcePath, destinationPath);
        }

        public override bool DoCommit(
            string authorName,
            string authorEmail,
            string comment,
            DateTime utcTime)
        {
            if (utcTime.Kind != DateTimeKind.Utc)
            {
                throw new ArgumentException($"Specified time {utcTime} is not Utc", nameof(utcTime));
            }

            string args = "commit";
            AddComment(comment, ref args, out TempFile? commentFile);

            using (commentFile)
            {
                ProcessStartInfo startInfo = GetStartInfo(args);
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

        public override void Tag(
            string name,
            string taggerName,
            string taggerEmail,
            string comment,
            DateTime utcTime)
        {
            if (utcTime.Kind != DateTimeKind.Utc)
            {
                throw new ArgumentException($"Specified time {utcTime} is not Utc", nameof(utcTime));
            }

            string args = "tag";
            AddComment(comment, ref args, out TempFile? commentFile);

            // tag names are not quoted because they cannot contain whitespace or quotes
            args += " -- " + name;

            using (commentFile)
            {
                ProcessStartInfo startInfo = GetStartInfo(args);
                startInfo.EnvironmentVariables["GIT_COMMITTER_NAME"] = taggerName;
                startInfo.EnvironmentVariables["GIT_COMMITTER_EMAIL"] = taggerEmail;
                startInfo.EnvironmentVariables["GIT_COMMITTER_DATE"] = GetUtcTimeString(utcTime);

                ExecuteUnless(startInfo, null);
            }
        }
    };
}
