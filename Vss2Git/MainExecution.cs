using System;
using System.Collections.Generic;
using System.Text;
using System.Reflection;
using Hpdi.VssLogicalLib;

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

        private readonly WorkQueue workQueue = new WorkQueue(1);
        private Logger logger = Logger.Null;
        private RevisionAnalyzer revisionAnalyzer;
        private ChangesetBuilder changesetBuilder;
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
            logger = string.IsNullOrEmpty(filename) ? Logger.Null : new Logger(filename);

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
            logger = Logger.Null;
        }

        public void StartConversion(bool runningUnderCommandLine = false)
        {
            try
            {
                OpenLog(Settings.LogFile, runningUnderCommandLine);

                logger.WriteLine("VSS2Git version {0}", Assembly.GetExecutingAssembly().GetName().Version);

                Encoding encoding = Encoding.GetEncoding(Settings.Encoding);

                logger.WriteLine("VSS encoding: {0} (CP: {1}, IANA: {2})",
                    encoding.EncodingName, encoding.CodePage, encoding.WebName);
                logger.WriteLine("Comment transcoding: {0}",
                    Settings.TranscodeComments ? "enabled" : "disabled");
                logger.WriteLine("Ignore errors: {0}",
                    Settings.IgnoreGitErrors ? "enabled" : "disabled");
                logger.WriteLine("Dry run: {0}",
                    Settings.DryRun ? "enabled" : "disabled");

                var df = new VssDatabaseFactory(Settings.VssDirectory)
                {
                    Encoding = encoding,
                };
                VssDatabase db = df.Open();

                string path = Settings.VssProject;
                VssItem item;
                try
                {
                    item = db.GetItem(path);
                }
                catch (VssPathException ex)
                {
                    // Invalid project path
                    VssUtil.MarkUnusedVariable(ref ex);
                    throw;
                }

                if (item is not VssProject project)
                {
                    throw new VssPathException($"{path} is not a project");
                }

                revisionAnalyzer = new RevisionAnalyzer(workQueue, logger, db);
#if false // #REVIEW
                if (!string.IsNullOrEmpty(Settings.VssExcludePaths))
                {
                    revisionAnalyzer.ExcludeFiles = Settings.VssExcludePaths;
                }
#endif
                revisionAnalyzer.AddItem(project);

                changesetBuilder = new ChangesetBuilder(workQueue, logger, revisionAnalyzer)
                {
                    AnyCommentThreshold = TimeSpan.FromSeconds(Settings.AnyCommentSeconds),
                    SameCommentThreshold = TimeSpan.FromSeconds(Settings.SameCommentSeconds),
                };
                changesetBuilder.BuildChangesets();
                logger.Flush();

                if (!string.IsNullOrEmpty(Settings.GitDirectory))
                {
                    var gitExporter = new GitExporter(workQueue, logger,
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

                    var git = new LibGit2SharpWrapper(Settings.GitDirectory, logger);
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
                    logger = Logger.Null;
                };

                workQueue.ExceptionThrown += LogException;
            }
            catch (Exception ex)
            {
                VssUtil.MarkUnusedVariable(ref ex);

                CloseLog();
                throw;
            }
        }

        private void LogException(object sender, ExceptionThrownEventArgs e)
        {
            string message = ExceptionFormatter.Format(e.Exception);

            logger.WriteLine("ERROR: {0}", message);
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
        public DateTime ElapsedTime => new DateTime(workQueue.ActiveTime.Ticks);
        public ICollection<Exception> WorkQueueExceptions => workQueue.FetchExceptions();
        public bool IsWorkQueueIdle =>
            workQueue != null ? workQueue.IsIdle : false;
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
