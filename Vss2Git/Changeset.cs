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
using SourceSafe.Logical;

namespace Hpdi.Vss2Git
{
    /// <summary>
    /// Represents a set of revisions made by a particular person at a particular time.
    /// </summary>
    /// <author>Trevor Robinson</author>
    [System.Diagnostics.DebuggerDisplay("{Id} {DateTime} {User} {Revisions.Count} {TargetFiles.Count}")]
    sealed class Changeset
    {
        public int Id { get; set; } = 0;
        public DateTime DateTime { get; set; }
        public string User { get; set; }
        public List<string> Comment { get; set; } = [];
        public List<Revision> Revisions { get; } = [];
        public HashSet<string> TargetFiles { get; } = [];
#if DEBUG
        // I added this mainly for my own tracing purposes, for debugging Hpdi.Vss2Git.ChangesetBuilder.BuildChangesets
        private Dictionary<string, List<VssActionType>> TargetFileActions { get; } = [];
#endif // DEBUG

        public void AddTargetFile(string targetFile, VssActionType actionType)
        {
            if (TargetFiles.Add(targetFile))
#if DEBUG
            {
                TargetFileActions[targetFile] = [];
            }
            TargetFileActions[targetFile].Add(actionType);
#else // !DEBUG
            {
            }
#endif // DEBUG
        }

        public bool ContainsTargetFile(string targetFile)
        {
            return TargetFiles.Contains(targetFile);
        }
    };
}
