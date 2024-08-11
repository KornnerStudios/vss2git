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
using System.IO;
using System.Text;
using SourceSafe;
using SourceSafe.Logical;
using SourceSafe.Physical.Files;
using SourceSafe.Physical.Records;

namespace Hpdi.VssLogicalLib
{
    /// <summary>
    /// Represents a VSS database and provides access to the items it contains.
    /// </summary>
    /// <author>Trevor Robinson</author>
    public sealed class VssDatabase
    {
        private readonly SourceSafe.Physical.Files.Names.VssNamesDatFile nameFile;

        public string BasePath { get; init; }

        public string IniPath { get; init; }

        public string DataPath { get; init; }

        public VssProject RootProject { get; init; }

        public Encoding Encoding { get; init; }

        public VssItem GetItem(string logicalPath)
        {
            string[] segments = logicalPath.Split(new char[] { SourceSafeConstants.ProjectSeparatorChar },
                StringSplitOptions.RemoveEmptyEntries);
            int index = segments[0] == SourceSafeConstants.RootProjectName ? 1 : 0;
            VssProject project = RootProject;
            while (index < segments.Length)
            {
                string name = segments[index++];

                VssProject subproject = project.FindProject(name);
                if (subproject != null)
                {
                    project = subproject;
                    continue;
                }

                VssFile file = project.FindFile(name);
                if (file != null)
                {
                    if (index == segments.Length)
                    {
                        return file;
                    }
                    else
                    {
                        string currentPath = string.Join(SourceSafeConstants.ProjectSeparator, segments, 0, index);
                        throw new VssPathException($"{currentPath} is not a project");
                    }
                }

                throw new VssPathException($"{name} not found in {project.LogicalPath}");
            }
            return project;
        }

        public VssItem GetItemByPhysicalName(string physicalName)
        {
            physicalName = physicalName.ToUpperInvariant();

            if (physicalName == SourceSafeConstants.RootPhysicalFile)
            {
                return RootProject;
            }

            string physicalPath = GetDataPath(physicalName);
            VssPhysicalFile physicalFile = new(physicalPath, Encoding);
            bool isProject = physicalFile.Header.IsProject;
            string logicalName = GetFullName(physicalFile.Header.Name);
            VssItemName itemName = new(logicalName, physicalName, isProject);
            VssItem item;
            if (isProject)
            {
                string parentFile = ((VssItemProjectHeaderRecord)physicalFile.Header).ParentFile;
                var parent = (VssProject)GetItemByPhysicalName(parentFile);
                string logicalPath = BuildPath(parent, logicalName);
                item = new VssProject(this, itemName, physicalPath, logicalPath);
            }
            else
            {
                item = new VssFile(this, itemName, physicalPath);
            }
            item.PhysicalFile = physicalFile;
            return item;
        }

        [Obsolete("Unused")]
        public bool PhysicalFileExists(string physicalName)
        {
            string physicalPath = GetDataPath(physicalName);
            return File.Exists(physicalPath);
        }

        internal VssDatabase(string path, Encoding encoding)
        {
            this.BasePath = path;
            this.Encoding = encoding;

            IniPath = Path.Combine(path, SourceSafeConstants.IniFile);
            SourceSafe.IO.SimpleIniReader iniReader = new(IniPath);
            iniReader.Parse();

            DataPath = Path.Combine(path, iniReader.GetValue("Data_Path", "data"));

            string namesPath = Path.Combine(DataPath, "names.dat");
            nameFile = new SourceSafe.Physical.Files.Names.VssNamesDatFile(namesPath, encoding);
            nameFile.ReadHeaderAndNames();

            RootProject = OpenProject(null, SourceSafeConstants.RootPhysicalFile, SourceSafeConstants.RootProjectName);
        }

        internal VssProject OpenProject(VssProject parent, string physicalNameAllUpperCase, string logicalName)
        {
            VssItemName itemName = new(logicalName, physicalNameAllUpperCase, true);
            string logicalPath = BuildPath(parent, logicalName);
            string physicalPath = GetDataPath(physicalNameAllUpperCase);
            return new VssProject(this, itemName, physicalPath, logicalPath);
        }

        internal VssFile OpenFile(string physicalNameAllUpperCase, string logicalName)
        {
            VssItemName itemName = new(logicalName, physicalNameAllUpperCase, false);
            string physicalPath = GetDataPath(physicalNameAllUpperCase);
            return new VssFile(this, itemName, physicalPath);
        }

        private static string BuildPath(VssProject parent, string logicalName)
        {
            return (parent != null) ? parent.LogicalPath + SourceSafeConstants.ProjectSeparator + logicalName : logicalName;
        }

        internal string GetDataPath(string physicalName)
        {
            return Path.Combine(Path.Combine(DataPath, physicalName.Substring(0, 1)), physicalName);
        }

        internal string GetFullName(SourceSafe.Physical.VssName name)
        {
            if (name.NameFileOffset != 0)
            {
                string projectOrLongName = nameFile.TryAndGetProjectOrLongName(name.NameFileOffset, name.IsProject);
                if (projectOrLongName != null)
                {
                    return projectOrLongName;
                }
            }
            return name.ShortName;
        }

        internal VssItemName GetItemName(SourceSafe.Physical.VssName name, string physicalName)
        {
            return new VssItemName(GetFullName(name), physicalName, name.IsProject);
        }
    }
}
