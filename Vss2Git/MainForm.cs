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

namespace Hpdi.Vss2Git
{
    /// <summary>
    /// Main form for the application.
    /// </summary>
    /// <author>Trevor Robinson</author>
    public partial class MainForm : Form
    {
        private readonly Dictionary<int, EncodingInfo> codePages = [];

        public MainForm()
        {
            InitializeComponent();
        }

        private void GoButton_Click(object sender, EventArgs e)
        {
            try
            {
                WriteStoredSettings();
                PopulateExecutionSettings();

                SourceSafe.GitConversion.Wrappers.AbstractGitWrapper git =
                    new GitExeWrapper(string.Empty, null);
                while (!git.FindExecutable())
                {
                    DialogResult button = MessageBox.Show("Git not found in PATH. " +
                        "If you need to modify your PATH variable, please " +
                        "restart the program for the changes to take effect.",
                        "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                MainExecution.Instance.StartConversion();

                statusTimer.Enabled = true;
                goButton.Enabled = false;
            }
            catch (Exception ex)
            {
                ShowException(ex);
            }
        }

        private void CancelButton_Click(object sender, EventArgs e)
        {
            MainExecution.Instance.WorkQueueAbort();

            this.Close();
        }

        private void StatusTimer_Tick(object sender, EventArgs e)
        {
            MainExecution mainExec = MainExecution.Instance;

            statusLabel.Text = mainExec.WorkQueueLastStatus ?? "Idle";
            timeLabel.Text = string.Format("Elapsed: {0:HH:mm:ss}",
                mainExec.ElapsedTime);

            fileLabel.Text = $"Files: {mainExec.RevisionAnalyzerFileCount}";
            revisionLabel.Text = $"Revisions: {mainExec.RevisionAnalyzerRevisionCount}";
            changeLabel.Text = $"Changesets: {mainExec.ChangesetCount}";

            if (mainExec.IsWorkQueueIdle)
            {
                mainExec.NullifyObjects();

                statusTimer.Enabled = false;
                goButton.Enabled = true;
            }

            ICollection<Exception> exceptions = mainExec.WorkQueueExceptions;
            if (exceptions != null)
            {
                foreach (Exception exception in exceptions)
                {
                    ShowException(exception);
                }
            }
        }

        private static void ShowException(Exception exception)
        {
            string message = SourceSafe.Exceptions.ExceptionFormatter.Format(exception);

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

            ReadStoredSettings();
        }

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            WriteStoredSettings();

            MainExecution.Instance.WorkQueueAbort();
            MainExecution.Instance.WorkQueueWaitIdle();
        }

        private void ReadStoredSettings()
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

        private void WriteStoredSettings()
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

        private void PopulateExecutionSettings()
        {
            MainSettings settings = MainExecution.Instance.Settings;
            settings.VssDirectory = vssDirTextBox.Text;
            settings.VssProject = vssProjectTextBox.Text;
            settings.VssExcludePaths = excludeTextBox.Text;
            settings.GitDirectory = outDirTextBox.Text;
            settings.DefaultEmailDomain = domainTextBox.Text;
            settings.EmailDictionaryFile = emailDictFileTextBox.Text;
            settings.DefaultComment = commentTextBox.Text;
            settings.LogFile = logTextBox.Text;
            settings.TranscodeComments = transcodeCheckBox.Checked;
            settings.ForceAnnotatedTags = forceAnnotatedCheckBox.Checked;
            settings.IgnoreGitErrors = ignoreErrorsCheckBox.Checked;
            settings.AnyCommentSeconds = (int)anyCommentUpDown.Value;
            settings.SameCommentSeconds = (int)sameCommentUpDown.Value;
            settings.IncludeVssMetaDataInComments = includeVssMetaDataInCommentsCheckBox.Checked;
            settings.IncludeIgnoredFiles = includeIgnoredFilesCheckbox.Checked;
            settings.DryRun = dryRunCheckBox.Checked;

            EncodingInfo encodingInfo;
            if (codePages.TryGetValue(encodingComboBox.SelectedIndex, out encodingInfo))
            {
                settings.Encoding = encodingInfo.CodePage;
            }
            else
            {
                settings.Encoding = Encoding.Default.CodePage;
            }
        }

        static private string FormatCodePageDescription(EncodingInfo info)
        {
            return $"CP{info.CodePage} - {info.DisplayName}";
        }
    }
}
