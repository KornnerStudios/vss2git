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
using System.Linq;
using System.Reflection;
using System.Text;
using System.Windows.Forms;
using Hpdi.VssLogicalLib;

namespace Hpdi.Vss2Git
{
    /// <summary>
    /// Main form for the application.
    /// </summary>
    /// <author>Trevor Robinson</author>
    public partial class MainForm : Form
    {
        private readonly Dictionary<int, EncodingInfo> codePages = new Dictionary<int, EncodingInfo>();
        private readonly WorkQueue workQueue = new WorkQueue(1);
        private Logger logger = Logger.Null;
        private RevisionAnalyzer revisionAnalyzer;
        private ChangesetBuilder changesetBuilder;

        public MainForm()
        {
            InitializeComponent();
        }

        private void OpenLog(string filename)
        {
            logger = string.IsNullOrEmpty(filename) ? Logger.Null : new Logger(filename);
        }

        private void CloseLog()
        {
            if (logger!=null)
                logger.Dispose();
            logger = Logger.Null;
        }

        private void goButton_Click(object sender, EventArgs e)
        {
            try
            {
                AbstractGitWrapper git = new GitExeWrapper(string.Empty, null);
                while (!git.FindExecutable())
                {
                    DialogResult button = MessageBox.Show("Git not found in PATH. " +
                        "If you need to modify your PATH variable, please " +
                        "restart the program for the changes to take effect.",
                        "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                OpenLog(logTextBox.Text);

                logger.WriteLine("VSS2Git version {0}", Assembly.GetExecutingAssembly().GetName().Version);

                WriteSettings();

                Encoding encoding = Encoding.Default;
                EncodingInfo encodingInfo;
                if (codePages.TryGetValue(encodingComboBox.SelectedIndex, out encodingInfo))
                {
                    encoding = encodingInfo.GetEncoding();
                }

                logger.WriteLine("VSS encoding: {0} (CP: {1}, IANA: {2})",
                    encoding.EncodingName, encoding.CodePage, encoding.WebName);
                logger.WriteLine("Comment transcoding: {0}",
                    transcodeCheckBox.Checked ? "enabled" : "disabled");
                logger.WriteLine("Ignore errors: {0}",
                    ignoreErrorsCheckBox.Checked ? "enabled" : "disabled");
                logger.WriteLine("Dry run: {0}",
                    dryRunCheckBox.Checked ? "enabled" : "disabled");

                var df = new VssDatabaseFactory(vssDirTextBox.Text);
                df.Encoding = encoding;
                VssDatabase db = df.Open();

                string path = VssDatabase.RootProjectName;
                VssItem item;
                try
                {
                    item = db.GetItem(path);
                }
                catch (VssPathException ex)
                {
                    MessageBox.Show(ex.Message, "Invalid project path",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                var project = item as VssProject;
                if (project == null)
                {
                    MessageBox.Show(path + " is not a project", "Invalid project path",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                revisionAnalyzer = new RevisionAnalyzer(workQueue, logger, db);
                revisionAnalyzer.AddItem(project);

                changesetBuilder = new ChangesetBuilder(workQueue, logger, revisionAnalyzer);
                changesetBuilder.AnyCommentThreshold = TimeSpan.FromSeconds((double)anyCommentUpDown.Value);
                changesetBuilder.SameCommentThreshold = TimeSpan.FromSeconds((double)sameCommentUpDown.Value);
                changesetBuilder.BuildChangesets();
                logger.Flush();

                if (!string.IsNullOrEmpty(outDirTextBox.Text))
                {
                    var gitExporter = new GitExporter(workQueue, logger,
                        revisionAnalyzer, changesetBuilder);
                    if (!string.IsNullOrEmpty(domainTextBox.Text))
                    {
                        gitExporter.EmailDomain = domainTextBox.Text;
                    }
                    if (!string.IsNullOrEmpty(commentTextBox.Text))
                    {
                        gitExporter.DefaultComment = commentTextBox.Text;
                    }
                    if (!string.IsNullOrEmpty(vssProjectTextBox.Text))
                    {
                        gitExporter.VssIncludedProjects = vssProjectTextBox.Text;
                    }
                    if (!string.IsNullOrEmpty(excludeTextBox.Text))
                    {
                        gitExporter.ExcludeFiles = excludeTextBox.Text;
                    }
                    gitExporter.IgnoreErrors = ignoreErrorsCheckBox.Checked;
                    gitExporter.DryRun = dryRunCheckBox.Checked;
                    gitExporter.UserToEmailDictionaryFile = emailDictFileTextBox.Text;
                    gitExporter.IncludeVssMetaDataInComments = includeVssMetaDataInCommentsCheckBox.Checked;

                    //git = new GitExeWrapper(outDirTextBox.Text, logger);
                    git = new LibGit2SharpWrapper(outDirTextBox.Text, logger);
                    if (!transcodeCheckBox.Checked)
                    {
                        git.CommitEncoding = encoding;
                    }

                    gitExporter.IncludeIgnoredFiles = includeIgnoredFilesCheckbox.Checked;

                    gitExporter.ExportToGit(git);
                }

                workQueue.Idle += delegate
                {
                    //logger.Dispose();
                    //logger = Logger.Null;
                };

                statusTimer.Enabled = true;
                goButton.Enabled = false;
            }
            catch (Exception ex)
            {
                ShowException(ex);

                CloseLog();
            }
        }

        private void cancelButton_Click(object sender, EventArgs e)
        {
            workQueue.Abort();

            this.Close();
        }

        private void statusTimer_Tick(object sender, EventArgs e)
        {
            statusLabel.Text = workQueue.LastStatus ?? "Idle";
            timeLabel.Text = string.Format("Elapsed: {0:HH:mm:ss}",
                new DateTime(workQueue.ActiveTime.Ticks));

            if (revisionAnalyzer != null)
            {
                fileLabel.Text = "Files: " + revisionAnalyzer.FileCount;
                revisionLabel.Text = "Revisions: " + revisionAnalyzer.RevisionCount;
            }

            if (changesetBuilder != null)
            {
                changeLabel.Text = "Changesets: " + changesetBuilder.Changesets.Count;
            }

            if (workQueue.IsIdle)
            {
                revisionAnalyzer = null;
                changesetBuilder = null;

                statusTimer.Enabled = false;
                goButton.Enabled = true;
            }

            ICollection<Exception> exceptions = workQueue.FetchExceptions();
            if (exceptions != null)
            {
                foreach (Exception exception in exceptions)
                {
                    ShowException(exception);
                }
            }
        }

        private void ShowException(Exception exception)
        {
            string message = ExceptionFormatter.Format(exception);
            logger.WriteLine("ERROR: {0}", message);
            logger.WriteLine(exception);

            MessageBox.Show(message, "Unhandled Exception",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        private void MainForm_Load(object sender, EventArgs e)
        {
            this.Text += " " + Assembly.GetExecutingAssembly().GetName().Version;

            int defaultCodePage = Encoding.Default.CodePage;
            string description = string.Format("System default - {0}", Encoding.Default.EncodingName);
            int defaultIndex = encodingComboBox.Items.Add(description);
            encodingComboBox.SelectedIndex = defaultIndex;

            EncodingInfo[] encodings = Encoding.GetEncodings();
            foreach (EncodingInfo encoding in encodings)
            {
                description = FormatCodePageDescription(encoding);
                int codePage = encoding.CodePage;
                int index = encodingComboBox.Items.Add(description);
                codePages[index] = encoding;
                if (codePage == defaultCodePage)
                {
                    codePages[defaultIndex] = encoding;
                }
            }

            ReadSettings();
        }

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            WriteSettings();

            workQueue.Abort();
            workQueue.WaitIdle();
        }

        private void ReadSettings()
        {
            Properties.Settings settings = Properties.Settings.Default;
            vssDirTextBox.Text = settings.VssDirectory;
            vssProjectTextBox.Text = settings.VssProject;
            excludeTextBox.Text = settings.VssExcludePaths;
            outDirTextBox.Text = settings.GitDirectory;
            domainTextBox.Text = settings.DefaultEmailDomain;
            emailDictFileTextBox.Text = settings.EmailDictionaryFile;
            logTextBox.Text = settings.LogFile;
            transcodeCheckBox.Checked = settings.TranscodeComments;
            forceAnnotatedCheckBox.Checked = settings.ForceAnnotatedTags;
            anyCommentUpDown.Value = settings.AnyCommentSeconds;
            sameCommentUpDown.Value = settings.SameCommentSeconds;
            includeVssMetaDataInCommentsCheckBox.Checked = settings.IncludeVssMetaDataInComments;

            if (!String.IsNullOrEmpty(settings.Encoding))
            {
                encodingComboBox.SelectedIndex = codePages.FirstOrDefault(x => x.Value.Name == settings.Encoding).Key;
            }
        }

        private void WriteSettings()
        {
            Properties.Settings settings = Properties.Settings.Default;
            settings.VssDirectory = vssDirTextBox.Text;
            settings.VssProject = vssProjectTextBox.Text;
            settings.VssExcludePaths = excludeTextBox.Text;
            settings.GitDirectory = outDirTextBox.Text;
            settings.DefaultEmailDomain = domainTextBox.Text;
            settings.EmailDictionaryFile = emailDictFileTextBox.Text;
            settings.LogFile = logTextBox.Text;
            settings.TranscodeComments = transcodeCheckBox.Checked;
            settings.ForceAnnotatedTags = forceAnnotatedCheckBox.Checked;
            settings.AnyCommentSeconds = (int)anyCommentUpDown.Value;
            settings.SameCommentSeconds = (int)sameCommentUpDown.Value;
            settings.IncludeVssMetaDataInComments = includeVssMetaDataInCommentsCheckBox.Checked;

            string encodingName = "";

            EncodingInfo[] encodings = Encoding.GetEncodings();
            foreach (EncodingInfo encoding in encodings)
            {
                string description = FormatCodePageDescription(encoding);

                if (encodingComboBox.SelectedItem.ToString() == description)
                {
                    encodingName = encoding.Name;
                    break;
                }
            }
            settings.Encoding = encodingName;

            settings.Save();
        }

        static private string FormatCodePageDescription(EncodingInfo info)
        {
            return string.Format("CP{0} - {1}", info.CodePage, info.DisplayName);
        }
    }
}
