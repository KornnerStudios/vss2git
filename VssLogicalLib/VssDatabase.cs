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
using Hpdi.VssPhysicalLib;
using SourceSafe;

namespace Hpdi.VssLogicalLib
{
    /// <summary>
    /// Represents a VSS database and provides access to the items it contains.
    /// </summary>
    /// <author>Trevor Robinson</author>
    public sealed class VssDatabase
    {
        private readonly NameFile nameFile;

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

        public VssItem GetItemPhysical(string physicalName)
        {
            physicalName = physicalName.ToUpper();

            if (physicalName == SourceSafeConstants.RootPhysicalFile)
            {
                return RootProject;
            }

            string physicalPath = GetDataPath(physicalName);
            ItemFile itemFile = new(physicalPath, Encoding);
            bool isProject = (itemFile.Header.ItemType == ItemType.Project);
            string logicalName = GetFullName(itemFile.Header.Name);
            VssItemName itemName = new(logicalName, physicalName, isProject);
            VssItem item;
            if (isProject)
            {
                string parentFile = ((ProjectHeaderRecord)itemFile.Header).ParentFile;
                var parent = (VssProject)GetItemPhysical(parentFile);
                string logicalPath = BuildPath(parent, logicalName);
                item = new VssProject(this, itemName, physicalPath, logicalPath);
            }
            else
            {
                item = new VssFile(this, itemName, physicalPath);
            }
            item.ItemFile = itemFile;
            return item;
        }

        [Obsolete("Unused")]
        public bool ItemExists(string physicalName)
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
            nameFile = new NameFile(namesPath, encoding);

            RootProject = OpenProject(null, SourceSafeConstants.RootPhysicalFile, SourceSafeConstants.RootProjectName);
        }

        internal VssProject OpenProject(VssProject parent, string physicalName, string logicalName)
        {
            VssItemName itemName = new(logicalName, physicalName, true);
            string logicalPath = BuildPath(parent, logicalName);
            string physicalPath = GetDataPath(physicalName);
            return new VssProject(this, itemName, physicalPath, logicalPath);
        }

        internal VssFile OpenFile(string physicalName, string logicalName)
        {
            VssItemName itemName = new(logicalName, physicalName, false);
            string physicalPath = GetDataPath(physicalName);
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

        internal string GetFullName(VssName name)
        {
            if (name.NameFileOffset != 0)
            {
                NameRecord nameRecord = nameFile.GetName(name.NameFileOffset);
                int nameIndex = nameRecord.IndexOf(name.IsProject ? NameKind.Project : NameKind.Long);
                if (nameIndex >= 0)
                {
                    return nameRecord.GetName(nameIndex);
                }
            }
            return name.ShortName;
        }

        internal VssItemName GetItemName(VssName name, string physicalName)
        {
            return new VssItemName(GetFullName(name), physicalName, name.IsProject);
        }
    }
}
