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
        private static MainExecution instance = null;
        private static readonly object padlock = new();

        private readonly TrackedWorkQueue workQueue = new(1);
        private SourceSafe.IO.SimpleLogger logger = SourceSafe.IO.SimpleLogger.Null;
        private VssRevisionAnalyzer revisionAnalyzer;
        private PseudoChangesetBuilder changesetBuilder;
        public MainSettings Settings { get; set; } = new();

        private MainExecution()
        {
        }

        public static MainExecution Instance
        {
            get
            {
                if (instance == null)
                {
                    lock (padlock)
                    {
                        if (instance == null)
                        {
                            instance = new MainExecution();
                        }
                    }
                }
                return instance;
            }
        }

        public List<Tuple<string, string>> ImportSettings(string filePath)
        {
            return Settings.ParseSettingsFile(filePath);
        }

        private void OpenLog(string filename, bool runningUnderCommandLine)
        {
            logger = string.IsNullOrEmpty(filename)
                ? SourceSafe.IO.SimpleLogger.Null
                : new(filename);

            if (runningUnderCommandLine)
            {
                // NOTE: this can end up modifying Logger.Null.
                // Logger.Dispose will null the echo writer, so always ensure we call Dispose!
                logger.EchoWriter = Console.Out;
            }
        }

        private void CloseLog()
        {
            logger?.Dispose();
            logger = SourceSafe.IO.SimpleLogger.Null;
        }

        public void StartConversion(bool runningUnderCommandLine = false)
        {
            try
            {
                OpenLog(Settings.LogFile, runningUnderCommandLine);

                logger.WriteLine("VSS2Git version {0}", Assembly.GetExecutingAssembly().GetName().Version);

                var encoding = Encoding.GetEncoding(Settings.Encoding);

                logger.WriteLine("VSS encoding: {0} (CP: {1}, IANA: {2})",
                    encoding.EncodingName, encoding.CodePage, encoding.WebName);
                logger.WriteLine("Comment transcoding: {0}",
                    Settings.TranscodeComments ? "enabled" : "disabled");
                logger.WriteLine("Ignore errors: {0}",
                    Settings.IgnoreGitErrors ? "enabled" : "disabled");
                logger.WriteLine("Dry run: {0}",
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

                revisionAnalyzer = new(workQueue, logger, db);
#if false // #REVIEW
                if (!string.IsNullOrEmpty(Settings.VssExcludePaths))
                {
                    revisionAnalyzer.ExcludeFiles = Settings.VssExcludePaths;
                }
#endif
                revisionAnalyzer.AddItem(project);

                changesetBuilder = new(workQueue, logger, revisionAnalyzer)
                {
                    AnyCommentThreshold = TimeSpan.FromSeconds(Settings.AnyCommentSeconds),
                    SameCommentThreshold = TimeSpan.FromSeconds(Settings.SameCommentSeconds),
                };
                changesetBuilder.BuildChangesets();
                logger.Flush();

                if (!string.IsNullOrEmpty(Settings.GitDirectory))
                {
                    var gitExporter = new SourceSafe.GitConversion.GitExporter(workQueue, logger,
                        revisionAnalyzer, changesetBuilder);
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

                    var git = new SourceSafe.GitConversion.Wrappers.LibGit2SharpWrapper(Settings.GitDirectory, logger);
                    if (!Settings.TranscodeComments)
                    {
                        git.CommitEncoding = encoding;
                    }

                    gitExporter.IncludeIgnoredFiles = Settings.IncludeIgnoredFiles;

                    gitExporter.ExportToGit(git);
                }

                workQueue.Idle += delegate
                {
                    logger.Dispose();
                    logger = SourceSafe.IO.SimpleLogger.Null;
                };

                workQueue.ExceptionThrown += LogException;
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

            logger.ErrorWriteLine(message);
            logger.WriteLine(e.Exception);
        }

        public void WorkQueueAbort()
        {
            workQueue.Abort();
        }

        public void WorkQueueWaitIdle()
        {
            workQueue.WaitIdle();
        }

        public string WorkQueueLastStatus => workQueue.LastStatus;
        public DateTime ElapsedTime => new(workQueue.ActiveTime.Ticks);
        public ICollection<Exception> WorkQueueExceptions => workQueue.FetchExceptions();
        public bool IsWorkQueueIdle =>
            workQueue == null || workQueue.IsIdle;
        public int RevisionAnalyzerFileCount =>
            revisionAnalyzer != null ? revisionAnalyzer.FileCount : 0;
        public int RevisionAnalyzerRevisionCount =>
            revisionAnalyzer != null ? revisionAnalyzer.RevisionCount : 0;
        public int ChangesetCount =>
            changesetBuilder != null ? changesetBuilder.Changesets.Count : 0;

        public void NullifyObjects()
        {
            revisionAnalyzer = null;
            changesetBuilder = null;
        }
    };
}
