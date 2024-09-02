using System;
using System.Collections.Generic;
using System.Text;
using System.Reflection;
using SourceSafe;
using SourceSafe.Analysis;
using SourceSafe.Jobs;
using SourceSafe.Logical.Items;

namespace Hpdi.Vss2Git
{
    /// <summary>
    /// Singleton execution manager
    /// </summary>
    /// <author>Brad Williams</author>
    sealed class MainExecution
    {
        private static MainExecution mInstance = null;
        private static readonly object mPadlock = new();

        private readonly TrackedWorkQueue mWorkQueue = new(1);
        private SourceSafe.IO.SimpleLogger mLogger = SourceSafe.IO.SimpleLogger.Null;
        private VssRevisionAnalyzer mRevisionAnalyzer;
        private PseudoChangesetBuilder mChangesetBuilder;
        public MainSettings Settings { get; set; } = new();

        private MainExecution()
        {
        }

        public static MainExecution Instance
        {
            get
            {
                if (mInstance == null)
                {
                    lock (mPadlock)
                    {
                        if (mInstance == null)
                        {
                            mInstance = new MainExecution();
                        }
                    }
                }
                return mInstance;
            }
        }

        public List<Tuple<string, string>> ImportSettings(
            string filePath)
        {
            return Settings.ParseSettingsFile(filePath);
        }

        private void OpenLog(
            string filename,
            bool runningUnderCommandLine)
        {
            mLogger = string.IsNullOrEmpty(filename)
                ? SourceSafe.IO.SimpleLogger.Null
                : new(filename);

            if (runningUnderCommandLine)
            {
                // NOTE: this can end up modifying Logger.Null.
                // Logger.Dispose will null the echo writer, so always ensure we call Dispose!
                mLogger.EchoWriter = Console.Out;
            }
        }

        private void CloseLog()
        {
            mLogger?.Dispose();
            mLogger = SourceSafe.IO.SimpleLogger.Null;
        }

        public void StartConversion(
            bool runningUnderCommandLine = false)
        {
            try
            {
                OpenLog(Settings.LogFile, runningUnderCommandLine);

                mLogger.WriteLine("VSS2Git version {0}", Assembly.GetExecutingAssembly().GetName().Version);

                var encoding = Encoding.GetEncoding(Settings.Encoding);

                mLogger.WriteLine("VSS encoding: {0} (CP: {1}, IANA: {2})",
                    encoding.EncodingName, encoding.CodePage, encoding.WebName);
                mLogger.WriteLine("Comment transcoding: {0}",
                    Settings.TranscodeComments ? "enabled" : "disabled");
                mLogger.WriteLine("Ignore errors: {0}",
                    Settings.IgnoreGitErrors ? "enabled" : "disabled");
                mLogger.WriteLine("Dry run: {0}",
                    Settings.DryRun ? "enabled" : "disabled");

                var dbConfig = new SourceSafe.Logical.VssDatabaseConfig();// #TODO
                SourceSafe.Logical.VssDatabase db = new(dbConfig, encoding, Settings.VssDirectory)
                {
                };

                string path = Settings.VssProject;
                VssItemBase item;
                try
                {
                    item = db.GetItem(path);
                }
                catch (SourceSafe.Logical.VssPathException ex)
                {
                    // Invalid project path
                    SourceSafeConstants.MarkUnusedVariable(ref ex);
                    throw;
                }

                if (item is not VssProjectItem project)
                {
                    throw new SourceSafe.Logical.VssPathException($"{path} is not a project");
                }

                mRevisionAnalyzer = new(mWorkQueue, mLogger, db);
#if false // #REVIEW
                if (!string.IsNullOrEmpty(Settings.VssExcludePaths))
                {
                    mRevisionAnalyzer.ExcludeFiles = Settings.VssExcludePaths;
                }
#endif
                mRevisionAnalyzer.AddItem(project);

                mChangesetBuilder = new(mWorkQueue, mLogger, mRevisionAnalyzer)
                {
                    AnyCommentThreshold = TimeSpan.FromSeconds(Settings.AnyCommentSeconds),
                    SameCommentThreshold = TimeSpan.FromSeconds(Settings.SameCommentSeconds),
                };
                mChangesetBuilder.BuildChangesets();
                mLogger.Flush();

                if (!string.IsNullOrEmpty(Settings.GitDirectory))
                {
                    var gitExporter = new SourceSafe.GitConversion.GitExporter(mWorkQueue, mLogger,
                        mRevisionAnalyzer, mChangesetBuilder);
                    if (!string.IsNullOrEmpty(Settings.DefaultEmailDomain))
                    {
                        gitExporter.EmailDomain = Settings.DefaultEmailDomain;
                    }
                    if (!string.IsNullOrEmpty(Settings.DefaultComment))
                    {
                        gitExporter.DefaultComment = Settings.DefaultComment;
                    }
                    if (!string.IsNullOrEmpty(Settings.VssProject))
                    {
                        gitExporter.VssIncludedProjects = Settings.VssProject;
                    }
                    if (!string.IsNullOrEmpty(Settings.VssExcludePaths))
                    {
                        gitExporter.ExcludeFiles = Settings.VssExcludePaths;
                    }
                    gitExporter.IgnoreErrors = Settings.IgnoreGitErrors;
                    gitExporter.DryRun = Settings.DryRun;
                    gitExporter.UserToEmailDictionaryFile = Settings.EmailDictionaryFile;
                    gitExporter.IncludeVssMetaDataInComments = Settings.IncludeVssMetaDataInComments;

                    var git = new SourceSafe.GitConversion.Wrappers.LibGit2SharpWrapper(Settings.GitDirectory, mLogger);
                    if (!Settings.TranscodeComments)
                    {
                        git.CommitEncoding = encoding;
                    }

                    gitExporter.IncludeIgnoredFiles = Settings.IncludeIgnoredFiles;

                    gitExporter.ExportToGit(git);
                }

                mWorkQueue.Idle += delegate
                {
                    mLogger.Dispose();
                    mLogger = SourceSafe.IO.SimpleLogger.Null;
                };

                mWorkQueue.ExceptionThrown += LogException;
            }
            catch (Exception ex)
            {
                SourceSafeConstants.MarkUnusedVariable(ref ex);

                CloseLog();
                throw;
            }
        }

        private void LogException(object sender, WorkerExceptionThrownEventArgs e)
        {
            string message = SourceSafe.Exceptions.ExceptionFormatter.Format(e.Exception);

            mLogger.ErrorWriteLine(message);
            mLogger.WriteLine(e.Exception);
        }

        public void WorkQueueAbort()
        {
            mWorkQueue.Abort();
        }

        public void WorkQueueWaitIdle()
        {
            mWorkQueue.WaitIdle();
        }

        public string WorkQueueLastStatus => mWorkQueue.LastStatus;
        public DateTime ElapsedTime => new(mWorkQueue.ActiveTime.Ticks);
        public ICollection<Exception> WorkQueueExceptions => mWorkQueue.FetchExceptions();
        public bool IsWorkQueueIdle =>
            mWorkQueue == null || mWorkQueue.IsIdle;
        public int RevisionAnalyzerFileCount =>
            mRevisionAnalyzer != null ? mRevisionAnalyzer.FileCount : 0;
        public int RevisionAnalyzerRevisionCount =>
            mRevisionAnalyzer != null ? mRevisionAnalyzer.RevisionCount : 0;
        public int ChangesetCount =>
            mChangesetBuilder != null ? mChangesetBuilder.Changesets.Count : 0;

        public void NullifyObjects()
        {
            mRevisionAnalyzer = null;
            mChangesetBuilder = null;
        }
    };
}
