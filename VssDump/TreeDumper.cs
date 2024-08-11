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
using SourceSafe.Logical.Items;

namespace Hpdi.VssDump
{
    /// <summary>
    /// Dumps the VSS project/file hierarchy to a text writer.
    /// </summary>
    /// <author>Trevor Robinson</author>
    class TreeDumper
    {
        private readonly TextWriter mWriter;
        public HashSet<string> PhysicalNames { get; } = [];

        public bool IncludeRevisions { get; set; }

        public TreeDumper(TextWriter writer)
        {
            this.mWriter = writer;
        }

        public void DumpProject(VssProjectItem project)
        {
            DumpProject(project, 0);
        }

        public void DumpProject(VssProjectItem project, int indent)
        {
            string indentStr = SourceSafe.IO.OutputUtil.GetIndentString(indent);

            PhysicalNames.Add(project.PhysicalName);
            mWriter.Write(indentStr);
            mWriter.WriteLine($"({project.PhysicalName}) {project.Name}/");

            foreach (VssProjectItem subproject in project.Projects)
            {
                DumpProject(subproject, indent + 1);
            }

            foreach (VssFileItem file in project.Files)
            {
                PhysicalNames.Add(file.PhysicalName);
                mWriter.Write(indentStr);
                mWriter.WriteLine($"\t({file.PhysicalName}) {file.Name} - {file.GetPath(project)}");

                if (IncludeRevisions)
                {
                    foreach (VssFileItemRevision version in file.Revisions)
                    {
                        mWriter.Write(indentStr);
                        mWriter.WriteLine($"\t\t#{version.Version} {version.User} {version.DateTime}");
                    }
                }
            }
        }
    };
}
