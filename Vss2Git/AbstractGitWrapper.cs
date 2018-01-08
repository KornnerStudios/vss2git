﻿/* Copyright 2009 HPDI, LLC
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
        private readonly Logger logger = null;
        private readonly Stopwatch stopwatch = new Stopwatch();
        private bool needsCommit = false;

        public Logger Logger
        {
            get { return logger; }
        }

        public Stopwatch Stopwatch
        {
            get { return stopwatch; }
        }
    
        private bool shellQuoting = false;
        public bool ShellQuoting
        {
            get { return shellQuoting; }
            set { shellQuoting = value; }
        }

        private Encoding commitEncoding = Encoding.UTF8;
        public Encoding CommitEncoding
        {
            get { return commitEncoding; }
            set { commitEncoding = value; }
        }

        public AbstractGitWrapper(string repoPath, Logger logger)
        {
            this.repoPath = repoPath;
            this.logger = logger;
        }

        protected static string GetUtcTimeString(DateTime utcTime)
        {
            if (utcTime.Kind != DateTimeKind.Utc)
            {
                throw new ArgumentException(String.Format("Specified time {0} is not Utc", utcTime), "utcTime");
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
                if (path.StartsWith("\\") || path.StartsWith("/"))
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
                (shellQuoting && (c == '&' || c == '|' || c == '<' || c == '>' || c == '^' || c == '%'));
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
            return stopwatch.Elapsed; 
        }

        public abstract bool FindExecutable();
        public abstract void Init(bool resetRepo);
        public abstract void Exit();
        public abstract void Configure();
        public abstract bool Add(string path);
        public abstract bool Add(IEnumerable<string> paths);
        public abstract bool AddDir(string path);
        public abstract bool AddAll();
        public abstract void RemoveFile(string path);
        public abstract void RemoveDir(string path, bool recursive);
        public abstract void RemoveEmptyDir(string path);
        public abstract void Move(string sourcePath, string destPath);
        public abstract void MoveEmptyDir(string sourcePath, string destPath);
        public bool Commit(string authorName, string authorEmail, string comment, DateTime utcTime)
        {
            if (utcTime.Kind != DateTimeKind.Utc)
            {
                throw new ArgumentException(String.Format("Specified time {0} is not Utc", utcTime), "utcTime");
            }

            if (!needsCommit)
            {
                return false;
            }

            needsCommit = false;

            return DoCommit(authorEmail, authorEmail, comment, utcTime);
        }
        public abstract void Tag(string name, string taggerName, string taggerEmail, string comment, DateTime utcTime);
        
    }
}
