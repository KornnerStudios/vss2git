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
using System.Diagnostics;
using System.Text;

namespace Hpdi.Vss2Git
{
    /// <summary>
    /// Wraps common execution of Git.
    /// </summary>
    /// <author>Dariusz Bywalec</author>
    abstract class AbstractGitWrapper : IGitWrapper
    {
        private readonly string repoPath = "";
        private bool needsCommit = false;
        public const string DefaultCheckoutBranch = "main"; // default Git repo branch

        public bool IncludeIgnoredFiles { get; set; }

        public SourceSafe.IO.SimpleLogger Logger { get; } = null;

        public Stopwatch Stopwatch { get; } = new Stopwatch();
        public bool ShellQuoting { get; set; } = false;

        public Encoding CommitEncoding { get; set; } = Encoding.UTF8;

        public AbstractGitWrapper(string repoPath, SourceSafe.IO.SimpleLogger logger)
        {
            this.repoPath = repoPath;
            Logger = logger;
        }

        protected static string GetUtcTimeString(DateTime utcTime)
        {
            if (utcTime.Kind != DateTimeKind.Utc)
            {
                throw new ArgumentException($"Specified time {utcTime} is not Utc", nameof(utcTime));
            }

            // format time according to ISO 8601 (avoiding locale-dependent month/day names)
            return utcTime.ToString("yyyy'-'MM'-'dd HH':'mm':'ss +0000");
        }

        private const char QuoteChar = '"';
        private const char EscapeChar = '\\';

        protected string QuoteRelativePath(string path)
        {
            if (path.StartsWith(repoPath))
            {
                path = path.Substring(repoPath.Length);
                if (path.StartsWith('\\') || path.StartsWith('/'))
                {
                    path = path.Substring(1);
                }
            }
            return Quote(path);
        }
        /// <summary>
        /// Puts quotes around a command-line argument if it includes whitespace
        /// or quotes.
        /// </summary>
        /// <remarks>
        /// There are two things going on in this method: quoting and escaping.
        /// Quoting puts the entire string in quotes, whereas escaping is per-
        /// character. Quoting happens only if necessary, when whitespace or a
        /// quote is encountered somewhere in the string, and escaping happens
        /// only within quoting. Spaces don't need escaping, since that's what
        /// the quotes are for. Slashes don't need escaping because apparently a
        /// backslash is only interpreted as an escape when it precedes a quote.
        /// Otherwise both slash and backslash are just interpreted as directory
        /// separators.
        /// </remarks>
        /// <param name="arg">A command-line argument to quote.</param>
        /// <returns>The given argument, possibly in quotes, with internal
        /// quotes escaped with backslashes.</returns>
        protected string Quote(string arg)
        {
            if (string.IsNullOrEmpty(arg))
            {
                return "\"\"";
            }

            StringBuilder buf = null;
            for (int i = 0; i < arg.Length; ++i)
            {
                char c = arg[i];
                if (buf == null && NeedsQuoting(c))
                {
                    buf = new StringBuilder(arg.Length + 2);
                    buf.Append(QuoteChar);
                    buf.Append(arg, 0, i);
                }
                if (buf != null)
                {
                    if (c == QuoteChar)
                    {
                        buf.Append(EscapeChar);
                    }
                    buf.Append(c);
                }
            }
            if (buf != null)
            {
                buf.Append(QuoteChar);
                return buf.ToString();
            }
            return arg;
        }

        private bool NeedsQuoting(char c)
        {
            return char.IsWhiteSpace(c) || c == QuoteChar ||
                (ShellQuoting && (c == '&' || c == '|' || c == '<' || c == '>' || c == '^' || c == '%'));
        }

        public abstract bool DoCommit(string authorName, string authorEmail, string comment, DateTime utcTime);

        public string GetRepoPath()
        {
            return repoPath;
        }
        public bool NeedsCommit()
        {
            return needsCommit;
        }
        public void SetNeedsCommit()
        {
            needsCommit = true;
        }

        TimeSpan IGitWrapper.ElapsedTime()
        {
            return Stopwatch.Elapsed;
        }

        public abstract bool FindExecutable();
        public abstract void Init(bool resetRepo);
        public abstract void Exit();
        public abstract void Configure();

        public string GetCheckoutBranch()
        {
            return DefaultCheckoutBranch;
        }

        public abstract bool Add(string path);
        public abstract bool Add(IEnumerable<string> paths);
        public abstract bool AddDir(string path);
        public abstract bool AddAll();
        public abstract void RemoveFile(string path);
        public abstract void RemoveDir(string path, bool recursive);
        public abstract void RemoveEmptyDir(string path);
        public abstract void MoveFile(string sourcePath, string destPath);
        public abstract void MoveDir(string sourcePath, string destPath);
        public abstract void MoveEmptyDir(string sourcePath, string destPath);
        public bool Commit(string authorName, string authorEmail, string comment, DateTime utcTime)
        {
            if (utcTime.Kind != DateTimeKind.Utc)
            {
                throw new ArgumentException($"Specified time {utcTime} is not Utc", nameof(utcTime));
            }

            if (!needsCommit)
            {
                return false;
            }

            needsCommit = false;

            return DoCommit(authorName, authorEmail, comment, utcTime);
        }
        public abstract void Tag(string name, string taggerName, string taggerEmail, string comment, DateTime utcTime);

    }
}
