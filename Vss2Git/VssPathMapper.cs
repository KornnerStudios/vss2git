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
using System.Diagnostics;
using System.IO;
using SourceSafe;
using SourceSafe.Logical;

namespace Hpdi.Vss2Git
{
    /// <summary>
    /// Class for representing VSS pin
    /// </summary>
    /// <author>Trevor Robinson</author>
    sealed class VssPin
    {
        public bool Pinned { get; }

        public int Revision { get; }

        public bool Destroyed { get; set; }

        public VssPin(bool pinned, int revision, bool destroyed)
        {
            Pinned = pinned;
            Revision = revision;
            Destroyed = destroyed;
        }

        public override string ToString()
        {
            if (Pinned)
            {
                return $"{"Pinned"} at {Revision}";
            }
            else
            {
                return "Unpinned";
            }
        }
    }

    /// <summary>
    /// Base class for representing VSS items.
    /// </summary>
    /// <author>Trevor Robinson</author>
    abstract class VssItemInfo
    {
        public string PhysicalName { get; }
        public string LogicalName { get; set; }

        protected VssItemInfo(string physicalName, string logicalName)
        {
            PhysicalName = physicalName;
            LogicalName = logicalName;
        }
    }

    /// <summary>
    /// Represents the current state of a VSS project.
    /// </summary>
    /// <author>Trevor Robinson</author>
    sealed class VssProjectInfo : VssItemInfo
    {
        private VssProjectInfo parentInfo;
        public VssProjectInfo Parent
        {
            get { return parentInfo; }
            set
            {
                if (parentInfo != value)
                {
                    if (parentInfo != null)
                    {
                        parentInfo.RemoveItem(this);
                    }
                    parentInfo = value;
                    if (parentInfo != null)
                    {
                        parentInfo.AddItem(this);
                    }
                }
            }
        }

        public bool IsRoot { get; set; }
        public string OriginalVssPath { get; set; }
        public string OriginalWorkDirPath { get; set; }

        public bool IsRooted
        {
            get
            {
                VssProjectInfo project = this;
                while (project.parentInfo != null)
                {
                    project = project.parentInfo;
                }
                return project.IsRoot;
            }
        }

        private readonly LinkedList<VssItemInfo> items = new();
        public IEnumerable<VssItemInfo> Items => items;

        public bool Destroyed { get; set; } = false;

        public VssProjectInfo(string physicalName, string logicalName)
            : base(physicalName, logicalName)
        {
        }

        public List<string> GetWorkDirPath()
        {
            List<string> path = null;

            if (IsRooted)
            {
                if (parentInfo != null)
                {
                    path = parentInfo.GetWorkDirPath();
                    path.Add(LogicalName);
                }
                else
                {
                    path = new List<string>()
                    {
                        OriginalWorkDirPath
                    };
                }
            }

            return path;
        }

        public List<string> GetLogicalPath()
        {
            List<string> path = null;

            if (IsRooted)
            {
                if (parentInfo != null)
                {
                    path = parentInfo.GetLogicalPath();
                    path.Add(LogicalName);
                }
                else
                {
                    path = new List<string>()
                    {
                        OriginalVssPath
                    };
                }
            }

            return path;
        }

        public bool IsSameOrSubproject(VssProjectInfo parentInfo)
        {
            VssProjectInfo project = this;
            while (project != null)
            {
                if (project == parentInfo)
                {
                    return true;
                }
                project = project.parentInfo;
            }
            return false;
        }

        public void AddItem(VssItemInfo item)
        {
            items.AddLast(item);
        }

        public void RemoveItem(VssItemInfo item)
        {
            items.Remove(item);
        }

        public bool ContainsLogicalName(string logicalName)
        {
            foreach (VssItemInfo item in items)
            {
                if (item.LogicalName.Equals(logicalName))
                {
                    return true;
                }
            }
            return false;
        }

        public bool ContainsFiles()
        {
            var subprojects = new LinkedList<VssProjectInfo>();
            VssProjectInfo project = this;
            while (project != null)
            {
                foreach (VssItemInfo item in project.items)
                {
                    if (item is VssProjectInfo subproject)
                    {
                        subprojects.AddLast(subproject);
                    }
                    else
                    {
                        return true;
                    }
                }
                if (subprojects.First != null)
                {
                    project = subprojects.First.Value;
                    subprojects.RemoveFirst();
                }
                else
                {
                    project = null;
                }
            }
            return false;
        }

        public IEnumerable<VssFileInfo> GetAllFiles()
        {
            var subprojects = new LinkedList<VssProjectInfo>();
            VssProjectInfo project = this;
            while (project != null)
            {
                foreach (VssItemInfo item in project.items)
                {
                    if (item is VssProjectInfo subproject)
                    {
                        subprojects.AddLast(subproject);
                    }
                    else
                    {
                        yield return (VssFileInfo)item;
                    }
                }
                if (subprojects.First != null)
                {
                    project = subprojects.First.Value;
                    subprojects.RemoveFirst();
                }
                else
                {
                    project = null;
                }
            }
        }

        public IEnumerable<VssProjectInfo> GetAllProjects()
        {
            var subprojects = new LinkedList<VssProjectInfo>();
            VssProjectInfo project = this;
            while (project != null)
            {
                foreach (VssItemInfo item in project.items)
                {
                    if (item is VssProjectInfo subproject)
                    {
                        subprojects.AddLast(subproject);
                        yield return subproject;
                    }
                }
                if (subprojects.First != null)
                {
                    project = subprojects.First.Value;
                    subprojects.RemoveFirst();
                }
                else
                {
                    project = null;
                }
            }
        }
    }

    /// <summary>
    /// Represents the current state of a VSS file.
    /// </summary>
    /// <author>Trevor Robinson</author>
    sealed class VssFileInfo : VssItemInfo
    {
        private readonly Dictionary<VssProjectInfo, VssPin> projects = [];
        public IEnumerable<KeyValuePair<VssProjectInfo, VssPin>> Projects => projects;

        private readonly Dictionary<VssProjectInfo, bool> destroyedInProjects = [];
        public IEnumerable<KeyValuePair<VssProjectInfo, bool>> DestroyedInProjects => destroyedInProjects;

        public int Version { get; set; } = 1;

        public VssFileInfo(string physicalName, string logicalName)
            : base(physicalName, logicalName)
        {
        }

        public void AddProject(VssProjectInfo project, bool destroyed)
        {
            projects[project] = new VssPin(false, -1, destroyed);
        }

        public void AddProject(VssProjectInfo project, int revision, bool destroyed)
        {
            projects[project] = new VssPin(true, revision, destroyed);
        }

        public void RemoveProject(VssProjectInfo project)
        {
            projects.Remove(project);
        }

        public VssPin GetProjectPin(VssProjectInfo project)
        {
            projects.TryGetValue(project, out VssPin pin);
            return pin;
        }
    }

    /// <summary>
    /// Tracks the names and locations of VSS projects and files as revisions are replayed.
    /// </summary>
    /// <author>Trevor Robinson</author>
    sealed class VssPathMapper
    {
        // keyed by physical name
        private readonly Dictionary<string, VssProjectInfo> physicalNameToProjectInfo = [];
        private readonly Dictionary<string, VssProjectInfo> physicalNameToRootInfo = [];
        private readonly Dictionary<string, VssFileInfo> physicalNameToFileInfo = [];

        public bool IsProjectRoot(string projectPhysicalName)
        {
            if (physicalNameToProjectInfo.TryGetValue(projectPhysicalName, out VssProjectInfo projectInfo))
            {
                return projectInfo.IsRoot;
            }
            return false;
        }

        public bool IsProjectRooted(string projectPhysicalName)
        {
            if (physicalNameToProjectInfo.TryGetValue(projectPhysicalName, out VssProjectInfo projectInfo))
            {
                return projectInfo.IsRooted;
            }
            return false;
        }

        public List<string> GetProjectWorkDirPath(string projectPhysicalName)
        {
            if (physicalNameToProjectInfo.TryGetValue(projectPhysicalName, out VssProjectInfo projectInfo))
            {
                return projectInfo.GetWorkDirPath();
            }
            return null;
        }

        public List<string> GetProjectLogicalPath(string projectPhysicalName)
        {
            if (physicalNameToProjectInfo.TryGetValue(projectPhysicalName, out VssProjectInfo projectInfo))
            {
                return projectInfo.GetLogicalPath();
            }
            return null;
        }

        public void SetProjectPath(string projectPhysicalName, string workDirPath, string originalVssPath)
        {
            var projectInfo = new VssProjectInfo(projectPhysicalName, originalVssPath)
            {
                IsRoot = true,
                OriginalVssPath = originalVssPath,
                OriginalWorkDirPath = workDirPath,
            };
            physicalNameToProjectInfo[projectPhysicalName] = projectInfo;
            physicalNameToRootInfo[projectPhysicalName] = projectInfo;
        }

        public IEnumerable<VssFileInfo> GetAllFiles(string projectPhysicalName)
        {
            if (physicalNameToProjectInfo.TryGetValue(projectPhysicalName, out VssProjectInfo projectInfo))
            {
                return projectInfo.GetAllFiles();
            }
            return null;
        }

        public IEnumerable<VssProjectInfo> GetAllProjects(string projectPhysicalName)
        {
            if (physicalNameToProjectInfo.TryGetValue(projectPhysicalName, out VssProjectInfo projectInfo))
            {
                return projectInfo.GetAllProjects();
            }
            return null;
        }

        public IEnumerable<Tuple<List<string>, List<string>>> GetFilePaths(
            string filePhysicalName,
            string underProject,
            int version,
            Logger logger)
        {
            var result = new List<Tuple<List<string>, List<string>>>();
            if (physicalNameToFileInfo.TryGetValue(filePhysicalName, out VssFileInfo fileInfo))
            {
                VssProjectInfo underProjectInfo = null;
                if (underProject != null)
                {
                    if (!physicalNameToProjectInfo.TryGetValue(underProject, out underProjectInfo))
                    {
                        return result;
                    }
                }
                foreach (KeyValuePair<VssProjectInfo, VssPin> project in fileInfo.Projects)
                {
                    if (underProjectInfo == null || project.Key.IsSameOrSubproject(underProjectInfo))
                    {
                        // ignore projects that are not rooted
                        List<string> projectLogicalPath = project.Key.GetLogicalPath();
                        List<string> projectWorkDirPath = project.Key.GetWorkDirPath();
                        if (projectLogicalPath != null && projectWorkDirPath != null)
                        {
                            var logicalPath = new List<string>(projectLogicalPath)
                            {
                                fileInfo.LogicalName
                            };

                            if (!project.Value.Destroyed)
                            {
                                if (!project.Value.Pinned || version <= project.Value.Revision)
                                {
                                    var workDirPath = new List<string>(projectWorkDirPath)
                                    {
                                        fileInfo.LogicalName
                                    };

                                    result.Add(new Tuple<List<string>, List<string>>(logicalPath, workDirPath));
                                }
                            }
                            else
                            {
                                logger.WriteLine("NOTE: Skipping destroyed file: {0}", LogicalPathToString(logicalPath));
                            }
                        }
                    }
                }
            }
            return result;
        }

        public int GetFileVersion(VssItemName project, string filePhysicalName)
        {
            int version = 1;

            if (physicalNameToFileInfo.TryGetValue(filePhysicalName, out VssFileInfo fileInfo) )
            {
                version = fileInfo.Version;

                VssProjectInfo projectInfo = GetOrCreateProject(project);

                if ( null != projectInfo )
                {
                    VssPin pin = fileInfo.GetProjectPin(projectInfo);

                    if (null != pin && pin.Pinned && version > pin.Revision)
                    {
                        version = pin.Revision;
                    }
                }
            }

            return version;
        }

        public bool GetFileDestroyed(VssItemName project, string filePhysicalName)
        {
            bool destroyed = false;

            if (physicalNameToFileInfo.TryGetValue(filePhysicalName, out VssFileInfo fileInfo))
            {
                VssProjectInfo projectInfo = GetOrCreateProject(project);

                if (null != projectInfo)
                {
                    VssPin pin = fileInfo.GetProjectPin(projectInfo);

                    if (null != pin)
                    {
                        destroyed = pin.Destroyed;
                    }
                }

            }

            return destroyed;
        }

        public void SetFileVersion(VssItemName name, int version)
        {
            VssFileInfo fileInfo = GetOrCreateFile(name);
            fileInfo.Version = version;
        }

        public VssItemInfo CreateItem(VssItemName project)
        {
            return GetOrCreateProject(project);
        }

        public VssItemInfo AddItem(VssItemName project, VssItemName name, bool destroyed)
        {
            VssProjectInfo parentInfo = GetOrCreateProject(project);
            VssItemInfo itemInfo;
            if (name.IsProject)
            {
                VssProjectInfo projectInfo = GetOrCreateProject(name);
                projectInfo.Destroyed = destroyed;
                projectInfo.Parent = parentInfo;
                itemInfo = projectInfo;
            }
            else
            {
                VssFileInfo fileInfo = GetOrCreateFile(name);
                fileInfo.AddProject(parentInfo, destroyed);
                parentInfo.AddItem(fileInfo);
                itemInfo = fileInfo;
            }

            // update name of item in case it was created on demand by
            // an earlier unmapped item that was subsequently renamed
            itemInfo.LogicalName = name.LogicalName;

            return itemInfo;
        }

        public VssItemInfo AddItem(VssItemName project, VssItemName name, bool destroyed, int pinnedRevision)
        {
            VssProjectInfo parentInfo = GetOrCreateProject(project);
            VssItemInfo itemInfo;
            if (name.IsProject)
            {
                VssProjectInfo projectInfo = GetOrCreateProject(name);
                projectInfo.Destroyed = destroyed;
                projectInfo.Parent = parentInfo;
                itemInfo = projectInfo;
            }
            else
            {
                VssFileInfo fileInfo = GetOrCreateFile(name);
                fileInfo.AddProject(parentInfo, pinnedRevision, destroyed);
                parentInfo.AddItem(fileInfo);
                itemInfo = fileInfo;
            }

            // update name of item in case it was created on demand by
            // an earlier unmapped item that was subsequently renamed
            itemInfo.LogicalName = name.LogicalName;

            return itemInfo;
        }

        public VssItemInfo RenameItem(VssItemName name)
        {
            VssItemInfo itemInfo;
            if (name.IsProject)
            {
                itemInfo = GetOrCreateProject(name);
            }
            else
            {
                itemInfo = GetOrCreateFile(name);
            }
            itemInfo.LogicalName = name.LogicalName;
            return itemInfo;
        }

        public VssItemInfo DeleteItem(VssItemName project, VssItemName name, bool destroyed)
        {
            VssProjectInfo parentInfo = GetOrCreateProject(project);
            VssItemInfo itemInfo;
            if (name.IsProject)
            {
                VssProjectInfo projectInfo = GetOrCreateProject(name);
                projectInfo.Destroyed = destroyed;
                projectInfo.Parent = null;
                itemInfo = projectInfo;
            }
            else
            {
                VssFileInfo fileInfo = GetOrCreateFile(name);
                fileInfo.RemoveProject(parentInfo);
                if (destroyed)
                {
                    fileInfo.AddProject(parentInfo, destroyed);
                }
                parentInfo.RemoveItem(fileInfo);
                itemInfo = fileInfo;
            }
            return itemInfo;
        }

        public VssItemInfo RecoverItem(VssItemName project, VssItemName name, bool destroyed)
        {
            VssProjectInfo parentInfo = GetOrCreateProject(project);
            VssItemInfo itemInfo;
            if (name.IsProject)
            {
                VssProjectInfo projectInfo = GetOrCreateProject(name);
                projectInfo.Destroyed = destroyed;
                projectInfo.Parent = parentInfo;
                itemInfo = projectInfo;
            }
            else
            {
                VssFileInfo fileInfo = GetOrCreateFile(name);
                fileInfo.AddProject(parentInfo, destroyed);
                parentInfo.AddItem(fileInfo);
                itemInfo = fileInfo;
            }
            return itemInfo;
        }

        public VssItemInfo PinItem(VssItemName project, VssItemName name, int revision, bool destroyed)
        {
            // pinning removes the project from the list of
            // sharing projects, so it no longer receives edits
            //return DeleteItem(project, name);

            VssProjectInfo parentInfo = GetOrCreateProject(project);
            VssItemInfo itemInfo;
            if (name.IsProject)
            {
                VssProjectInfo projectInfo = GetOrCreateProject(name);
                projectInfo.Destroyed = destroyed;
                projectInfo.Parent = null;
                itemInfo = projectInfo;
            }
            else
            {
                VssFileInfo fileInfo = GetOrCreateFile(name);

                fileInfo.RemoveProject(parentInfo);
                parentInfo.RemoveItem(fileInfo);
                fileInfo.AddProject(parentInfo, revision, destroyed);
                parentInfo.AddItem(fileInfo);

                itemInfo = fileInfo;
            }
            return itemInfo;
        }

        public VssItemInfo UnpinItem(VssItemName project, VssItemName name, bool destroyed)
        {
            // unpinning restores the project to the list of
            // sharing projects, so it receives edits
            VssProjectInfo parentInfo = GetOrCreateProject(project);
            VssItemInfo itemInfo;
            if (name.IsProject)
            {
                VssProjectInfo projectInfo = GetOrCreateProject(name);
                projectInfo.Destroyed = destroyed;
                projectInfo.Parent = parentInfo;
                itemInfo = projectInfo;
            }
            else
            {
                VssFileInfo fileInfo = GetOrCreateFile(name);
                fileInfo.RemoveProject(parentInfo);
                parentInfo.RemoveItem(fileInfo);

                fileInfo.AddProject(parentInfo, destroyed);
                parentInfo.AddItem(fileInfo);

                itemInfo = fileInfo;
            }
            return itemInfo;
        }

        public VssItemInfo BranchFile(VssItemName project, VssItemName newName, VssItemName oldName, bool newDestroyed)
        {
            Debug.Assert(!newName.IsProject);
            Debug.Assert(!oldName.IsProject);

            // "branching a file" (in VSS parlance) essentially moves it from
            // one project to another (and could potentially change its name)
            VssProjectInfo parentInfo = GetOrCreateProject(project);

            // remove filename from old project
            VssFileInfo oldFile = GetOrCreateFile(oldName);
            // retain version number from old file
            VssPin oldPin = oldFile.GetProjectPin(parentInfo);

            oldFile.RemoveProject(parentInfo);
            parentInfo.RemoveItem(oldFile);

            // add filename to new project
            VssFileInfo newFile = GetOrCreateFile(newName);
            newFile.AddProject(parentInfo, newDestroyed);

            if (null != oldPin && oldPin.Pinned)
            {
                newFile.Version = oldPin.Revision;
            }
            else
            {
                newFile.Version = oldFile.Version;
            }

            parentInfo.AddItem(newFile);

            return newFile;
        }

        public VssProjectInfo MoveProjectFrom(VssItemName project, VssItemName subproject, string oldProjectSpec)
        {
            VssUtil.MarkUnusedVariable(ref oldProjectSpec);

            Debug.Assert(subproject.IsProject);

            VssProjectInfo parentInfo = GetOrCreateProject(project);
            VssProjectInfo subprojectInfo = GetOrCreateProject(subproject);
            subprojectInfo.Parent = parentInfo;
            return subprojectInfo;
        }

        public VssProjectInfo MoveProjectTo(VssItemName project, VssItemName subproject, string newProjectSpec)
        {
            VssUtil.MarkUnusedVariable(ref project);

            VssProjectInfo subprojectInfo = GetOrCreateProject(subproject);
            int lastSlash = newProjectSpec.LastIndexOf('/');
            if (lastSlash > 0)
            {
                string newParentSpec = newProjectSpec.Substring(0, lastSlash);
                VssProjectInfo parentInfo = ResolveProjectSpec(newParentSpec);
                if (parentInfo != null)
                {
                    // propagate the destroyed flag from the new parent
                    subprojectInfo.Parent = parentInfo;
                    subprojectInfo.Destroyed |= parentInfo.Destroyed;
                }
                else
                {
                    // if resolution fails, the target project has been destroyed
                    // or is outside the set of projects being mapped
                    subprojectInfo.Destroyed = true;
                }
            }
            return subprojectInfo;
        }

        public bool ProjectContainsLogicalName(VssItemName project, VssItemName name)
        {
            VssProjectInfo parentInfo = GetOrCreateProject(project);
            return parentInfo.ContainsLogicalName(name.LogicalName);
        }

        private VssProjectInfo GetOrCreateProject(VssItemName name)
        {
            if (!physicalNameToProjectInfo.TryGetValue(name.PhysicalName, out VssProjectInfo projectInfo))
            {
                projectInfo = new VssProjectInfo(name.PhysicalName, name.LogicalName);
                physicalNameToProjectInfo[name.PhysicalName] = projectInfo;
            }
            return projectInfo;
        }

        private VssFileInfo GetOrCreateFile(VssItemName name)
        {
            if (!physicalNameToFileInfo.TryGetValue(name.PhysicalName, out VssFileInfo fileInfo))
            {
                fileInfo = new VssFileInfo(name.PhysicalName, name.LogicalName);
                physicalNameToFileInfo[name.PhysicalName] = fileInfo;
            }
            return fileInfo;
        }

        private VssProjectInfo ResolveProjectSpec(string projectSpec)
        {
            if (!projectSpec.StartsWith(SourceSafeConstants.ProjectSpecPrefix))
            {
                throw new ArgumentException($"Project spec must start with {SourceSafeConstants.ProjectSpecPrefix}", nameof(projectSpec));
            }

            foreach (VssProjectInfo rootInfo in physicalNameToRootInfo.Values)
            {
                if (projectSpec.StartsWith(rootInfo.OriginalVssPath))
                {
                    int rootLength = rootInfo.OriginalVssPath.Length;
                    if (!rootInfo.OriginalVssPath.EndsWith('/'))
                    {
                        ++rootLength;
                    }

                    // Fix the scenario where the projectSpec equals rootInfo.OriginalVssPath
                    // the root cannot have a parent
                    if (projectSpec.Equals(rootInfo.OriginalVssPath))
                    {
                        goto NotFound;
                    }

                    string subPath = projectSpec.Substring(rootLength);
                    string[] subprojectNames = subPath.Split('/');
                    VssProjectInfo projectInfo = rootInfo;
                    foreach (string subprojectName in subprojectNames)
                    {
                        bool found = false;
                        foreach (VssItemInfo item in projectInfo.Items)
                        {
                            if (item is VssProjectInfo subprojectInfo && subprojectInfo.LogicalName == subprojectName)
                            {
                                projectInfo = subprojectInfo;
                                found = true;
                                break;
                            }
                        }
                        if (!found)
                        {
                            goto NotFound;
                        }
                    }
                    return projectInfo;
                }
            }

        NotFound:
            return null;
        }

        public static string GetWorkingPath(string workingRoot, string vssPath)
        {
            if (vssPath == SourceSafeConstants.RootProjectName)
            {
                return workingRoot;
            }

            if (vssPath.StartsWith(SourceSafeConstants.ProjectSpecPrefix))
            {
                vssPath = vssPath.Substring(SourceSafeConstants.ProjectSpecPrefix.Length);
            }

            string relPath = vssPath.Replace(SourceSafeConstants.ProjectSeparatorChar, Path.DirectorySeparatorChar);
            return Path.Combine(workingRoot, relPath);
        }

        public static string LogicalPathToString(IEnumerable<string> path)
        {
            return String.Join(SourceSafeConstants.ProjectSeparator, path);
        }

        public static string WorkDirPathToString(IEnumerable<string> path)
        {
            string result = "";

            foreach (string p in path)
            {
                result = Path.Combine(result, p);
            }

            return result;
        }

        public const string RelativeWorkDirPrefix = "<WorkDir>";
        public static string RelativeWorkDirPathToString(IEnumerable<string> path)
        {
            string result = RelativeWorkDirPrefix;

            int index = 0;
            foreach (string p in path)
            {
                // index 0 should already be the literal WorkDir path, so skip it
                if (index != 0)
                {
                    result = Path.Combine(result, p);
                }

                index++;
            }

            return result;
        }
    }
}
