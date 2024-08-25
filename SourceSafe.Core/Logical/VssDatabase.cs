using System.Text;
using SourceSafe.Physical.Files;
using SourceSafe.Physical.Records;

namespace SourceSafe.Logical
{
    public sealed class VssDatabase
    {
        private readonly Physical.Files.Names.VssNamesDatFile mNameFile;
        private readonly HashSet<string> mKnownUserNames = [];

        public VssDatabaseConfig Config { get; }
        public Encoding Encoding { get; }

        public string BasePath { get; }

        public string IniPath { get; }
        public string UsersIniPath { get; }

        public string DataPath { get; }

        public IReadOnlySet<string> KnownUserNames => mKnownUserNames;

        public Items.VssProjectItem RootProject { get; }

        public Items.VssItemBase GetItem(string logicalPath)
        {
            string[] segments = logicalPath.Split(new char[] { SourceSafeConstants.ProjectSeparatorChar },
                StringSplitOptions.RemoveEmptyEntries);
            int index = segments[0] == SourceSafeConstants.RootProjectName ? 1 : 0;
            Items.VssProjectItem project = RootProject;
            while (index < segments.Length)
            {
                string name = segments[index++];

                Items.VssProjectItem? subproject = project.FindProject(name);
                if (subproject != null)
                {
                    project = subproject;
                    continue;
                }

                Items.VssFileItem? file = project.FindFile(name);
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

        public Items.VssItemBase GetItemByPhysicalName(string physicalName)
        {
            physicalName = physicalName.ToUpperInvariant();

            if (physicalName == SourceSafeConstants.RootPhysicalFile)
            {
                return RootProject;
            }

            string physicalPath = GetDataPath(physicalName);
            VssPhysicalFile physicalFile = new(this, physicalPath);
            bool isProject = physicalFile.Header.IsProject;
            string logicalName = GetFullName(physicalFile.Header.Name);
            VssItemName itemName = new(logicalName, physicalName, isProject);
            Items.VssItemBase item;
            if (isProject)
            {
                string parentFile = ((VssItemProjectHeaderRecord)physicalFile.Header).ParentFile;
                var parent = (Items.VssProjectItem)GetItemByPhysicalName(parentFile);
                string logicalPath = BuildPath(parent, logicalName);
                item = new Items.VssProjectItem(this, itemName, physicalPath, logicalPath);
            }
            else
            {
                item = new Items.VssFileItem(this, itemName, physicalPath);
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

        public VssDatabase(
            VssDatabaseConfig config,
            Encoding encoding,
            string repositoryPath)
        {
            Config = config;
            Encoding = encoding;

            Config.ForcePathCollectionsToExpectedFormat();

            // GetFullPath will also normalize the path (namely, use backslashes)
            BasePath = Path.GetFullPath(repositoryPath);
            if (!Path.EndsInDirectorySeparator(BasePath))
            {
                Path.Combine(BasePath, Path.DirectorySeparatorChar.ToString());
            }

            #region Read srcsafe.ini
            {
                IniPath = Path.Combine(repositoryPath, SourceSafeConstants.IniFile);
                IO.SimpleIniReader srcSafeIni = new(IniPath);
                srcSafeIni.Parse();

                string dataPathInIni = srcSafeIni.GetValue("Data_Path", "data");
                DataPath = Path.Combine(repositoryPath, dataPathInIni);
            }
            #endregion

            #region Read names.dat
            {
                string namesPath = Path.Combine(DataPath, SourceSafeConstants.NamesDatFile);
                mNameFile = new(this, namesPath);
                mNameFile.ReadHeaderAndNames();
            }
            #endregion

            #region Read users.txt
            {
                UsersIniPath = Path.Combine(repositoryPath, SourceSafeConstants.UsersIniFile);
                IO.SimpleIniReader usersIni = new(UsersIniPath);
                usersIni.Parse();

                // unlike the um.dat file, the users.txt entries are not alphabetized.
                // Example entry format: "Admin = users\admin\ss.ini".
                // #NOTE There may have been a bug in VSS where renaming a user added a new (empty) folder
                // with the new user name, but did not remove the old folder and instead saved to the
                // old user name's folder and ss.ini. I've seen this in at least one repository.
                if (usersIni.Entries.Count > 0)
                {
                    List<string> usersFromIni = [..usersIni.Entries.Keys];
                    usersFromIni.Sort();
                    mKnownUserNames = new(usersFromIni);
                }
            }
            #endregion

            RootProject = OpenProject(null, SourceSafeConstants.RootPhysicalFile, SourceSafeConstants.RootProjectName);
        }

        internal Items.VssProjectItem OpenProject(
            Items.VssProjectItem? parent,
            string physicalNameAllUpperCase,
            string logicalName)
        {
            VssItemName itemName = new(logicalName, physicalNameAllUpperCase, true);
            string logicalPath = BuildPath(parent, logicalName);
            string physicalPath = GetDataPath(physicalNameAllUpperCase);
            return new Items.VssProjectItem(this, itemName, physicalPath, logicalPath);
        }

        internal Items.VssFileItem OpenFile(
            string physicalNameAllUpperCase,
            string logicalName)
        {
            VssItemName itemName = new(logicalName, physicalNameAllUpperCase, false);
            string physicalPath = GetDataPath(physicalNameAllUpperCase);
            return new Items.VssFileItem(this, itemName, physicalPath);
        }

        private static string BuildPath(
            Items.VssProjectItem? parent,
            string logicalName)
        {
            return (parent != null)
                ? parent.LogicalPath + SourceSafeConstants.ProjectSeparator + logicalName
                : logicalName;
        }

        internal string GetDataPath(string physicalName)
        {
            // e.g., <repo>\data\y\yaaaaaaa
            string physicalNameFirstLetter = physicalName[..1];
            string physicalRootFolder = Path.Combine(DataPath, physicalNameFirstLetter);
            string physicalFilePath = Path.Combine(physicalRootFolder, physicalName);
            return physicalFilePath;
        }

        internal string GetDatabaseRelativePath(
            string fullPathInDatabase)
        {
            // Chop off the repository path
            string relativePath = Path.GetRelativePath(BasePath, fullPathInDatabase);
            return relativePath;
        }

        internal string GetActualFullFilePath(
            string fullDesiredPath)
        {
            string actualFullPath = fullDesiredPath;
            if (Config.FileRemapping?.Count > 0)
            {
                // Chop off the repository path and make ALL UPPERCASE
                string desiredRelativePath = GetDatabaseRelativePath(fullDesiredPath);
                desiredRelativePath = desiredRelativePath.ToUpperInvariant();

                // Find an ALL UPPERCASE path to override with a new path
                if (Config.FileRemapping.TryGetValue(desiredRelativePath, out string? remappedRelativePath))
                {
                    actualFullPath = Path.Combine(BasePath, remappedRelativePath);
                    actualFullPath = Path.GetFullPath(actualFullPath);
                }
            }
            return actualFullPath;
        }

        internal string GetFullName(Physical.VssName name)
        {
            if (name.NameFileOffset != 0)
            {
                string? projectOrLongName = mNameFile.TryAndGetProjectOrLongName(name.NameFileOffset, name.IsProject);
                if (projectOrLongName != null)
                {
                    return projectOrLongName;
                }
            }
            return name.ShortName;
        }

        internal VssItemName GetItemName(Physical.VssName name, string physicalName)
        {
            return new VssItemName(GetFullName(name), physicalName, name.IsProject);
        }
    };
}
