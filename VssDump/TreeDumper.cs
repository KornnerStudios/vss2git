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

using System.Collections.Generic;
using System.IO;
using Hpdi.VssLogicalLib;

namespace Hpdi.VssDump
{
    /// <summary>
    /// Dumps the VSS project/file hierarchy to a text writer.
    /// </summary>
    /// <author>Trevor Robinson</author>
    class TreeDumper
    {
        private readonly TextWriter writer;
        public HashSet<string> PhysicalNames { get; } = [];

        public bool IncludeRevisions { get; set; }

        public TreeDumper(TextWriter writer)
        {
            this.writer = writer;
        }

        public void DumpProject(VssProject project)
        {
            DumpProject(project, 0);
        }

        public void DumpProject(VssProject project, int indent)
        {
            string indentStr = VssPhysicalLib.VssRecord.DumpGetIndentString(indent);

            PhysicalNames.Add(project.PhysicalName);
            writer.Write(indentStr);
            writer.WriteLine($"({project.PhysicalName}) {project.Name}/");

            foreach (VssProject subproject in project.Projects)
            {
                DumpProject(subproject, indent + 1);
            }

            foreach (VssFile file in project.Files)
            {
                PhysicalNames.Add(file.PhysicalName);
                writer.Write(indentStr);
                writer.WriteLine($"\t({file.PhysicalName}) {file.Name} - {file.GetPath(project)}");

                if (IncludeRevisions)
                {
                    foreach (VssFileRevision version in file.Revisions)
                    {
                        writer.Write(indentStr);
                        writer.WriteLine($"\t\t#{version.Version} {version.User} {version.DateTime}");
                    }
                }
            }
        }
    }
}
