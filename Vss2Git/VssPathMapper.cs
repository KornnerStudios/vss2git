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
using System.IO;
using System.Text;
using Hpdi.VssLogicalLib;

namespace Hpdi.Vss2Git
{
    /// <summary>
    /// Class for representing VSS pin
    /// </summary>
    /// <author>Trevor Robinson</author>
    class VssPin
    {
        private readonly bool pinned;
        public bool Pinned
        {
            get { return pinned; }
        }

        private readonly int revision;
        public int Revision
        {
            get { return revision; }
        }

        private bool destroyed;
        public bool Destroyed
        {
            get { return destroyed; }
            set { destroyed = value; }
        }

        public VssPin(bool pinned, int revision, bool destroyed)
        {
            this.pinned = pinned;
            this.revision = revision;
            this.destroyed = destroyed;
        }

        public override string ToString()
        {
            if (pinned)
            {
                return string.Format("{0} at {1}", "Pinned", revision);
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
    class VssItemInfo
    {
        private readonly string physicalName;
        public string PhysicalName
        {
            get { return physicalName; }
        }

        private string logicalName;
        public string LogicalName
        {
            get { return logicalName; }
            set { logicalName = value; }
        }

        public VssItemInfo(string physicalName, string logicalName)
        {
            this.physicalName = physicalName;
            this.logicalName = logicalName;
        }
    }

    /// <summary>
    /// Represents the current state of a VSS project.
    /// </summary>
    /// <author>Trevor Robinson</author>
    class VssProjectInfo : VssItemInfo
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

        private bool isRoot;
        public bool IsRoot
        {
            get { return isRoot; }
            set { isRoot = value; }
        }

        // valid only for root paths; used to resolve project specifiers
        private string originalVssPath;
        public string OriginalVssPath
        {
            get { return originalVssPath; }
            set { originalVssPath = value; }
        }

        private string originalWorkDirPath;
        public string OriginalWorkDirPath
        {
            get { return originalWorkDirPath; }
            set { originalWorkDirPath = value; }
        }

        public bool IsRooted
        {
            get
            {
                var project = this;
                while (project.parentInfo != null)
                {
                    project = project.parentInfo;
                }
                return project.isRoot;
            }
        }

        private readonly LinkedList<VssItemInfo> items = new LinkedList<VssItemInfo>();
        public IEnumerable<VssItemInfo> Items
        {
            get { return items; }
        }

        private bool destroyed = false;
        public bool Destroyed
        {
            get { return destroyed; }
            set { destroyed = value; }
        }

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
                    path = new List<string>();
                    path.Add(OriginalWorkDirPath);
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
                    path = new List<string>();
                    path.Add(OriginalVssPath);
                }
            }

            return path;
        }

        public bool IsSameOrSubproject(VssProjectInfo parentInfo)
        {
            var project = this;
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
            foreach (var item in items)
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
            var project = this;
            while (project != null)
            {
                foreach (var item in project.items)
                {
                    var subproject = item as VssProjectInfo;
                    if (subproject != null)
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
            var project = this;
            while (project != null)
            {
                foreach (var item in project.items)
                {
                    var subproject = item as VssProjectInfo;
                    if (subproject != null)
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
            var project = this;
            while (project != null)
            {
                foreach (var item in project.items)
                {
                    var subproject = item as VssProjectInfo;
                    if (subproject != null)
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
    class VssFileInfo : VssItemInfo
    {
        private readonly Dictionary<VssProjectInfo, VssPin> projects = new Dictionary<VssProjectInfo, VssPin>();
        public IEnumerable<KeyValuePair<VssProjectInfo, VssPin>> Projects
        {
            get { return projects; }
        }

        private readonly Dictionary<VssProjectInfo, bool> destroyedInProjects = new Dictionary<VssProjectInfo, bool>();
        public IEnumerable<KeyValuePair<VssProjectInfo, bool>> DestroyedInProjects
        {
            get { return destroyedInProjects; }
        }

        private int version = 1;
        public int Version
        {
            get { return version; }
            set { version = value; }
        }

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
            VssPin pin = null;
            projects.TryGetValue(project, out pin);
            return pin;
        }
    }

    /// <summary>
    /// Tracks the names and locations of VSS projects and files as revisions are replayed.
    /// </summary>
    /// <author>Trevor Robinson</author>
    class VssPathMapper
    {
        // keyed by physical name
        private readonly Dictionary<string, VssProjectInfo> projectInfos = new Dictionary<string, VssProjectInfo>();
        private readonly Dictionary<string, VssProjectInfo> rootInfos = new Dictionary<string, VssProjectInfo>();
        private readonly Dictionary<string, VssFileInfo> fileInfos = new Dictionary<string, VssFileInfo>();

        public bool IsProjectRoot(string project)
        {
            VssProjectInfo projectInfo;
            if (projectInfos.TryGetValue(project, out projectInfo))
            {
                return projectInfo.IsRoot;
            }
            return false;
        }

        public bool IsProjectRooted(string project)
        {
            VssProjectInfo projectInfo;
            if (projectInfos.TryGetValue(project, out projectInfo))
            {
                return projectInfo.IsRooted;
            }
            return false;
        }

        public List<string> GetProjectWorkDirPath(string project)
        {
            VssProjectInfo projectInfo;
            if (projectInfos.TryGetValue(project, out projectInfo))
            {
                return projectInfo.GetWorkDirPath();
            }
            return null;
        }

        public List<string> GetProjectLogicalPath(string project)
        {
            VssProjectInfo projectInfo;
            if (projectInfos.TryGetValue(project, out projectInfo))
            {
                return projectInfo.GetLogicalPath();
            }
            return null;
        }

        public void SetProjectPath(string project, string workDirPath, string originalVssPath)
        {
            var projectInfo = new VssProjectInfo(project, originalVssPath);
            projectInfo.IsRoot = true;
            projectInfo.OriginalVssPath = originalVssPath;
            projectInfo.OriginalWorkDirPath = workDirPath;
            projectInfos[project] = projectInfo;
            rootInfos[project] = projectInfo;
        }

        public IEnumerable<VssFileInfo> GetAllFiles(string project)
        {
            VssProjectInfo projectInfo;
            if (projectInfos.TryGetValue(project, out projectInfo))
            {
                return projectInfo.GetAllFiles();
            }
            return null;
        }

        public IEnumerable<VssProjectInfo> GetAllProjects(string project)
        {
            VssProjectInfo projectInfo;
            if (projectInfos.TryGetValue(project, out projectInfo))
            {
                return projectInfo.GetAllProjects();
            }
            return null;
        }

        public IEnumerable<Tuple<List<string>, List<string>>> GetFilePaths(string file, string underProject, int version, Logger logger)
        {
            var result = new LinkedList<Tuple<List<string>, List<string>>>();
            VssFileInfo fileInfo;
            if (fileInfos.TryGetValue(file, out fileInfo))
            {
                VssProjectInfo underProjectInfo = null;
                if (underProject != null)
                {
                    if (!projectInfos.TryGetValue(underProject, out underProjectInfo))
                    {
                        return result;
                    }
                }
                foreach (var project in fileInfo.Projects)
                {
                    if (underProjectInfo == null || project.Key.IsSameOrSubproject(underProjectInfo))
                    {
                        // ignore projects that are not rooted
                        var projectLogicalPath = project.Key.GetLogicalPath();
                        var projectWorkDirPath = project.Key.GetWorkDirPath();
                        if (projectLogicalPath != null && projectWorkDirPath != null)
                        {
                            var logicalPath = new List<string>(projectLogicalPath);
                            logicalPath.Add(fileInfo.LogicalName);

                            if (!project.Value.Destroyed)
                            {
                                if (!project.Value.Pinned || version <= project.Value.Revision)
                                {
                                    var workDirPath = new List<string>(projectWorkDirPath);
                                    workDirPath.Add(fileInfo.LogicalName);

                                    result.AddLast(new Tuple<List<string>, List<string>>(logicalPath, workDirPath));
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

        public int GetFileVersion(VssItemName project, string file)
        {
            int version = 1;

            VssFileInfo fileInfo;
            if ( fileInfos.TryGetValue(file, out fileInfo) )
            {
                version = fileInfo.Version;

                var projectInfo = GetOrCreateProject(project);

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

        public bool GetFileDestroyed(VssItemName project, string file)
        {
            bool destroyed = false;

            VssFileInfo fileInfo;
            if (fileInfos.TryGetValue(file, out fileInfo))
            {
                var projectInfo = GetOrCreateProject(project);

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
            var parentInfo = GetOrCreateProject(project);
            VssItemInfo itemInfo;
            if (name.IsProject)
            {
                var projectInfo = GetOrCreateProject(name);
                projectInfo.Destroyed = destroyed;
                projectInfo.Parent = parentInfo;
                itemInfo = projectInfo;
            }
            else
            {
                var fileInfo = GetOrCreateFile(name);
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
            var parentInfo = GetOrCreateProject(project);
            VssItemInfo itemInfo;
            if (name.IsProject)
            {
                var projectInfo = GetOrCreateProject(name);
                projectInfo.Destroyed = destroyed;
                projectInfo.Parent = parentInfo;
                itemInfo = projectInfo;
            }
            else
            {
                var fileInfo = GetOrCreateFile(name);
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
            var parentInfo = GetOrCreateProject(project);
            VssItemInfo itemInfo;
            if (name.IsProject)
            {
                var projectInfo = GetOrCreateProject(name);
                projectInfo.Destroyed = destroyed;
                projectInfo.Parent = null;
                itemInfo = projectInfo;
            }
            else
            {
                var fileInfo = GetOrCreateFile(name);
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
            var parentInfo = GetOrCreateProject(project);
            VssItemInfo itemInfo;
            if (name.IsProject)
            {
                var projectInfo = GetOrCreateProject(name);
                projectInfo.Destroyed = destroyed;
                projectInfo.Parent = parentInfo;
                itemInfo = projectInfo;
            }
            else
            {
                var fileInfo = GetOrCreateFile(name);
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

            var parentInfo = GetOrCreateProject(project);
            VssItemInfo itemInfo;
            if (name.IsProject)
            {
                var projectInfo = GetOrCreateProject(name);
                projectInfo.Destroyed = destroyed;
                projectInfo.Parent = null;
                itemInfo = projectInfo;
            }
            else
            {
                var fileInfo = GetOrCreateFile(name);
            
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
            var parentInfo = GetOrCreateProject(project);
            VssItemInfo itemInfo;
            if (name.IsProject)
            {
                var projectInfo = GetOrCreateProject(name);
                projectInfo.Destroyed = destroyed;
                projectInfo.Parent = parentInfo;
                itemInfo = projectInfo;
            }
            else
            {
                var fileInfo = GetOrCreateFile(name);
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
            var parentInfo = GetOrCreateProject(project);

            // remove filename from old project
            var oldFile = GetOrCreateFile(oldName);
            // retain version number from old file
            var oldPin = oldFile.GetProjectPin(parentInfo);

            oldFile.RemoveProject(parentInfo);
            parentInfo.RemoveItem(oldFile);

            // add filename to new project
            var newFile = GetOrCreateFile(newName);
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
            Debug.Assert(subproject.IsProject);

            var parentInfo = GetOrCreateProject(project);
            var subprojectInfo = GetOrCreateProject(subproject);
            subprojectInfo.Parent = parentInfo;
            return subprojectInfo;
        }

        public VssProjectInfo MoveProjectTo(VssItemName project, VssItemName subproject, string newProjectSpec)
        {
            var subprojectInfo = GetOrCreateProject(subproject);
            var lastSlash = newProjectSpec.LastIndexOf('/');
            if (lastSlash > 0)
            {
                var newParentSpec = newProjectSpec.Substring(0, lastSlash);
                var parentInfo = ResolveProjectSpec(newParentSpec);
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
            var parentInfo = GetOrCreateProject(project);
            return parentInfo.ContainsLogicalName(name.LogicalName);
        }

        private VssProjectInfo GetOrCreateProject(VssItemName name)
        {
            VssProjectInfo projectInfo;
            if (!projectInfos.TryGetValue(name.PhysicalName, out projectInfo))
            {
                projectInfo = new VssProjectInfo(name.PhysicalName, name.LogicalName);
                projectInfos[name.PhysicalName] = projectInfo;
            }
            return projectInfo;
        }

        private VssFileInfo GetOrCreateFile(VssItemName name)
        {
            VssFileInfo fileInfo;
            if (!fileInfos.TryGetValue(name.PhysicalName, out fileInfo))
            {
                fileInfo = new VssFileInfo(name.PhysicalName, name.LogicalName);
                fileInfos[name.PhysicalName] = fileInfo;
            }
            return fileInfo;
        }

        private VssProjectInfo ResolveProjectSpec(string projectSpec)
        {
            if (!projectSpec.StartsWith("$/"))
            {
                throw new ArgumentException("Project spec must start with $/", "projectSpec");
            }

            foreach (var rootInfo in rootInfos.Values)
            {
                if (projectSpec.StartsWith(rootInfo.OriginalVssPath))
                {
                    var rootLength = rootInfo.OriginalVssPath.Length;
                    if (!rootInfo.OriginalVssPath.EndsWith("/"))
                    {
                        ++rootLength;
                    }

                    // Fix the scenario where the projectSpec equals rootInfo.OriginalVssPath 
                    // the root cannot have a parent
                    if (projectSpec.Equals(rootInfo.OriginalVssPath)) 
                        goto NotFound; 

                    var subpath = projectSpec.Substring(rootLength);
                    var subprojectNames = subpath.Split('/');
                    var projectInfo = rootInfo;
                    foreach (var subprojectName in subprojectNames)
                    {
                        var found = false;
                        foreach (var item in projectInfo.Items)
                        {
                            var subprojectInfo = item as VssProjectInfo;
                            if (subprojectInfo != null && subprojectInfo.LogicalName == subprojectName)
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
            if (vssPath == "$")
            {
                return workingRoot;
            }

            if (vssPath.StartsWith("$/"))
            {
                vssPath = vssPath.Substring(2);
            }

            var relPath = vssPath.Replace(VssDatabase.ProjectSeparatorChar, Path.DirectorySeparatorChar);
            return Path.Combine(workingRoot, relPath);
        }

        public string LogicalPathToString(IEnumerable<string> path)
        {
            return String.Join(VssDatabase.ProjectSeparator, path);
        }

        public string WorkDirPathToString(IEnumerable<string> path)
        {
            var result = "";

            foreach (var p in path)
            {
                result = Path.Combine(result, p);
            }

            return result;
        }
    }
}
