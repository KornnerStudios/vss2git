using System.Diagnostics;
using SourceSafe.Logical;

namespace SourceSafe.Analysis.PathMapping
{
    /// <summary>
    /// Tracks the names and locations of VSS projects and files as revisions are replayed.
    /// </summary>
    public sealed class VssPathMapper
    {
        private readonly Dictionary<string, VssProjectPathMapping> mPhysicalNameToProjectInfo = [];
        private readonly Dictionary<string, VssProjectPathMapping> mPhysicalNameToRootInfo = [];
        private readonly Dictionary<string, VssFilePathMapping> mPhysicalNameToFileInfo = [];

        public bool IsProjectRoot(
            string projectPhysicalName)
        {
            if (mPhysicalNameToProjectInfo.TryGetValue(projectPhysicalName, out VssProjectPathMapping? projectInfo))
            {
                return projectInfo.IsRoot;
            }
            return false;
        }

        public bool IsProjectRooted(
            string projectPhysicalName)
        {
            if (mPhysicalNameToProjectInfo.TryGetValue(projectPhysicalName, out VssProjectPathMapping? projectInfo))
            {
                return projectInfo.IsRooted;
            }
            return false;
        }

        public List<string>? GetProjectWorkDirPath(
            string projectPhysicalName)
        {
            if (mPhysicalNameToProjectInfo.TryGetValue(projectPhysicalName, out VssProjectPathMapping? projectInfo))
            {
                return projectInfo.GetWorkDirPath();
            }
            return null;
        }

        public List<string>? GetProjectLogicalPath(
            string projectPhysicalName)
        {
            if (mPhysicalNameToProjectInfo.TryGetValue(projectPhysicalName, out VssProjectPathMapping? projectInfo))
            {
                return projectInfo.GetLogicalPath();
            }
            return null;
        }

        public void SetProjectPath(
            string projectPhysicalName,
            string workDirPath,
            string originalVssPath)
        {
            var projectInfo = new VssProjectPathMapping(projectPhysicalName, originalVssPath)
            {
                IsRoot = true,
                OriginalVssPath = originalVssPath,
                OriginalWorkDirPath = workDirPath,
            };
            mPhysicalNameToProjectInfo[projectPhysicalName] = projectInfo;
            mPhysicalNameToRootInfo[projectPhysicalName] = projectInfo;
        }

        public IEnumerable<VssFilePathMapping>? GetAllFiles(
            string projectPhysicalName)
        {
            if (mPhysicalNameToProjectInfo.TryGetValue(projectPhysicalName, out VssProjectPathMapping? projectInfo))
            {
                return projectInfo.GetAllFiles();
            }
            return null;
        }

        public IEnumerable<VssProjectPathMapping>? GetAllProjects(
            string projectPhysicalName)
        {
            if (mPhysicalNameToProjectInfo.TryGetValue(projectPhysicalName, out VssProjectPathMapping? projectInfo))
            {
                return projectInfo.GetAllProjects();
            }
            return null;
        }

        public IEnumerable<Tuple<List<string>, List<string>>> GetFilePaths(
            string filePhysicalName,
            string underProject,
            int version,
            SourceSafe.IO.SimpleLogger logger)
        {
            var result = new List<Tuple<List<string>, List<string>>>();
            if (mPhysicalNameToFileInfo.TryGetValue(filePhysicalName, out VssFilePathMapping? fileInfo))
            {
                VssProjectPathMapping? underProjectInfo = null;
                if (underProject != null)
                {
                    if (!mPhysicalNameToProjectInfo.TryGetValue(underProject, out underProjectInfo))
                    {
                        return result;
                    }
                }
                foreach (KeyValuePair<VssProjectPathMapping, VssPinInfo> project in fileInfo.ProjectAndPinPairs)
                {
                    if (underProjectInfo == null || project.Key.IsSameOrSubproject(underProjectInfo))
                    {
                        // ignore projects that are not rooted
                        List<string>? projectLogicalPath = project.Key.GetLogicalPath();
                        List<string>? projectWorkDirPath = project.Key.GetWorkDirPath();
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

        public int GetFileVersion(
            VssItemName project,
            string filePhysicalName)
        {
            int version = 1;

            if (mPhysicalNameToFileInfo.TryGetValue(filePhysicalName, out VssFilePathMapping? fileInfo))
            {
                version = fileInfo.Version;
                VssProjectPathMapping projectInfo = GetOrCreateProject(project);

                if (projectInfo != null)
                {
                    VssPinInfo? pin = fileInfo.GetProjectPin(projectInfo);

                    if (pin != null && pin.Value.Pinned && version > pin.Value.Revision)
                    {
                        version = pin.Value.Revision;
                    }
                }
            }

            return version;
        }

        public bool GetFileDestroyed(
            VssItemName project,
            string filePhysicalName)
        {
            bool destroyed = false;

            if (mPhysicalNameToFileInfo.TryGetValue(filePhysicalName, out VssFilePathMapping? fileInfo))
            {
                VssProjectPathMapping projectInfo = GetOrCreateProject(project);

                if (projectInfo != null)
                {
                    VssPinInfo? pin = fileInfo.GetProjectPin(projectInfo);

                    if (pin != null)
                    {
                        destroyed = pin.Value.Destroyed;
                    }
                }

            }

            return destroyed;
        }

        public void SetFileVersion(
            VssItemName name,
            int version)
        {
            VssFilePathMapping fileInfo = GetOrCreateFile(name);
            fileInfo.Version = version;
        }

        public VssItemPathMappingBase CreateItem(
            VssItemName project)
        {
            return GetOrCreateProject(project);
        }

        public VssItemPathMappingBase AddItem(
            VssItemName project,
            VssItemName name,
            bool destroyed)
        {
            VssProjectPathMapping parentInfo = GetOrCreateProject(project);
            VssItemPathMappingBase itemInfo;
            if (name.IsProject)
            {
                VssProjectPathMapping projectInfo = GetOrCreateProject(name);
                projectInfo.Destroyed = destroyed;
                projectInfo.Parent = parentInfo;
                itemInfo = projectInfo;
            }
            else
            {
                VssFilePathMapping fileInfo = GetOrCreateFile(name);
                fileInfo.AddProject(parentInfo, destroyed);
                parentInfo.AddItem(fileInfo);
                itemInfo = fileInfo;
            }

            // update name of item in case it was created on demand by
            // an earlier unmapped item that was subsequently renamed
            itemInfo.LogicalName = name.LogicalName;

            return itemInfo;
        }

        public VssItemPathMappingBase AddItem(
            VssItemName project,
            VssItemName name,

            bool destroyed, int pinnedRevision)
        {
            VssProjectPathMapping parentInfo = GetOrCreateProject(project);
            VssItemPathMappingBase itemInfo;
            if (name.IsProject)
            {
                VssProjectPathMapping projectInfo = GetOrCreateProject(name);
                projectInfo.Destroyed = destroyed;
                projectInfo.Parent = parentInfo;
                itemInfo = projectInfo;
            }
            else
            {
                VssFilePathMapping fileInfo = GetOrCreateFile(name);
                fileInfo.AddProject(parentInfo, pinnedRevision, destroyed);
                parentInfo.AddItem(fileInfo);
                itemInfo = fileInfo;
            }

            // update name of item in case it was created on demand by
            // an earlier unmapped item that was subsequently renamed
            itemInfo.LogicalName = name.LogicalName;

            return itemInfo;
        }

        public VssItemPathMappingBase RenameItem(
            VssItemName name)
        {
            VssItemPathMappingBase itemInfo;
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

        public VssItemPathMappingBase DeleteItem(
            VssItemName project,
            VssItemName name,
            bool destroyed)
        {
            VssProjectPathMapping parentInfo = GetOrCreateProject(project);
            VssItemPathMappingBase itemInfo;
            if (name.IsProject)
            {
                VssProjectPathMapping projectInfo = GetOrCreateProject(name);
                projectInfo.Destroyed = destroyed;
                projectInfo.Parent = null;
                itemInfo = projectInfo;
            }
            else
            {
                VssFilePathMapping fileInfo = GetOrCreateFile(name);
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

        public VssItemPathMappingBase RecoverItem(
            VssItemName project,
            VssItemName name,
            bool destroyed)
        {
            VssProjectPathMapping parentInfo = GetOrCreateProject(project);
            VssItemPathMappingBase itemInfo;
            if (name.IsProject)
            {
                VssProjectPathMapping projectInfo = GetOrCreateProject(name);
                projectInfo.Destroyed = destroyed;
                projectInfo.Parent = parentInfo;
                itemInfo = projectInfo;
            }
            else
            {
                VssFilePathMapping fileInfo = GetOrCreateFile(name);
                fileInfo.AddProject(parentInfo, destroyed);
                parentInfo.AddItem(fileInfo);
                itemInfo = fileInfo;
            }
            return itemInfo;
        }

        public VssItemPathMappingBase PinItem(
            VssItemName project,
            VssItemName name,
            int revision,
            bool destroyed)
        {
            // pinning removes the project from the list of
            // sharing projects, so it no longer receives edits
            //return DeleteItem(project, name);

            VssProjectPathMapping parentInfo = GetOrCreateProject(project);
            VssItemPathMappingBase itemInfo;
            if (name.IsProject)
            {
                VssProjectPathMapping projectInfo = GetOrCreateProject(name);
                projectInfo.Destroyed = destroyed;
                projectInfo.Parent = null;
                itemInfo = projectInfo;
            }
            else
            {
                VssFilePathMapping fileInfo = GetOrCreateFile(name);

                fileInfo.RemoveProject(parentInfo);
                parentInfo.RemoveItem(fileInfo);
                fileInfo.AddProject(parentInfo, revision, destroyed);
                parentInfo.AddItem(fileInfo);

                itemInfo = fileInfo;
            }
            return itemInfo;
        }

        public VssItemPathMappingBase UnpinItem(
            VssItemName project,
            VssItemName name,
            bool destroyed)
        {
            // unpinning restores the project to the list of
            // sharing projects, so it receives edits
            VssProjectPathMapping parentInfo = GetOrCreateProject(project);
            VssItemPathMappingBase itemInfo;
            if (name.IsProject)
            {
                VssProjectPathMapping projectInfo = GetOrCreateProject(name);
                projectInfo.Destroyed = destroyed;
                projectInfo.Parent = parentInfo;
                itemInfo = projectInfo;
            }
            else
            {
                VssFilePathMapping fileInfo = GetOrCreateFile(name);
                fileInfo.RemoveProject(parentInfo);
                parentInfo.RemoveItem(fileInfo);

                fileInfo.AddProject(parentInfo, destroyed);
                parentInfo.AddItem(fileInfo);

                itemInfo = fileInfo;
            }
            return itemInfo;
        }

        public VssItemPathMappingBase BranchFile(
            VssItemName project,
            VssItemName newName,
            VssItemName oldName,
            bool newDestroyed)
        {
            Debug.Assert(!newName.IsProject);
            Debug.Assert(!oldName.IsProject);

            // "branching a file" (in VSS parlance) essentially moves it from
            // one project to another (and could potentially change its name)
            VssProjectPathMapping parentInfo = GetOrCreateProject(project);

            // remove filename from old project
            VssFilePathMapping oldFile = GetOrCreateFile(oldName);
            // retain version number from old file
            VssPinInfo? oldPin = oldFile.GetProjectPin(parentInfo);

            oldFile.RemoveProject(parentInfo);
            parentInfo.RemoveItem(oldFile);

            // add filename to new project
            VssFilePathMapping newFile = GetOrCreateFile(newName);
            newFile.AddProject(parentInfo, newDestroyed);

            if (oldPin != null && oldPin.Value.Pinned)
            {
                newFile.Version = oldPin.Value.Revision;
            }
            else
            {
                newFile.Version = oldFile.Version;
            }

            parentInfo.AddItem(newFile);

            return newFile;
        }

        public VssProjectPathMapping MoveProjectFrom(
            VssItemName project,
            VssItemName subproject,
            string oldProjectSpec)
        {
            SourceSafeConstants.MarkUnusedVariable(ref oldProjectSpec);

            Debug.Assert(subproject.IsProject);

            VssProjectPathMapping parentInfo = GetOrCreateProject(project);
            VssProjectPathMapping subprojectInfo = GetOrCreateProject(subproject);
            subprojectInfo.Parent = parentInfo;
            return subprojectInfo;
        }

        public VssProjectPathMapping MoveProjectTo(
            VssItemName project,
            VssItemName subproject,
            string newProjectSpec)
        {
            SourceSafeConstants.MarkUnusedVariable(ref project);

            VssProjectPathMapping subprojectInfo = GetOrCreateProject(subproject);
            int lastSlash = newProjectSpec.LastIndexOf('/');
            if (lastSlash > 0)
            {
                string newParentSpec = newProjectSpec[..lastSlash];
                VssProjectPathMapping? parentInfo = ResolveProjectSpec(newParentSpec);
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
            VssProjectPathMapping parentInfo = GetOrCreateProject(project);
            return parentInfo.ContainsLogicalName(name.LogicalName);
        }

        private VssProjectPathMapping GetOrCreateProject(VssItemName name)
        {
            if (!mPhysicalNameToProjectInfo.TryGetValue(name.PhysicalName, out VssProjectPathMapping? projectInfo))
            {
                projectInfo = new VssProjectPathMapping(name.PhysicalName, name.LogicalName);
                mPhysicalNameToProjectInfo[name.PhysicalName] = projectInfo;
            }
            return projectInfo;
        }

        private VssFilePathMapping GetOrCreateFile(VssItemName name)
        {
            if (!mPhysicalNameToFileInfo.TryGetValue(name.PhysicalName, out VssFilePathMapping? fileInfo))
            {
                fileInfo = new VssFilePathMapping(name.PhysicalName, name.LogicalName);
                mPhysicalNameToFileInfo[name.PhysicalName] = fileInfo;
            }
            return fileInfo;
        }

        private VssProjectPathMapping? ResolveProjectSpec(string projectSpec)
        {
            if (!projectSpec.StartsWith(SourceSafeConstants.ProjectSpecPrefix))
            {
                throw new ArgumentException($"Project spec must start with {SourceSafeConstants.ProjectSpecPrefix}", nameof(projectSpec));
            }

            foreach (VssProjectPathMapping rootInfo in mPhysicalNameToRootInfo.Values)
            {
                if (projectSpec.StartsWith(rootInfo.OriginalVssPath!))
                {
                    int rootLength = rootInfo.OriginalVssPath!.Length;
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

                    string subPath = projectSpec[rootLength..];
                    string[] subprojectNames = subPath.Split('/');
                    VssProjectPathMapping projectInfo = rootInfo;
                    foreach (string subprojectName in subprojectNames)
                    {
                        bool found = false;
                        foreach (VssItemPathMappingBase item in projectInfo.Items)
                        {
                            if (item is VssProjectPathMapping subprojectInfo && subprojectInfo.LogicalName == subprojectName)
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
                vssPath = vssPath[SourceSafeConstants.ProjectSpecPrefix.Length..];
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
    };
}
