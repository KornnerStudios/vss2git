using System.Text;
using SourceSafe.Physical.Files;
using SourceSafe.Physical.Records;

namespace SourceSafe.Logical
{
    public sealed class VssDatabase
    {
        private readonly Physical.Files.Names.VssNamesDatFile mNameFile;

        public string BasePath { get; init; }

        public string IniPath { get; init; }

        public string DataPath { get; init; }

        public Items.VssProjectItem RootProject { get; init; }

        public Encoding Encoding { get; init; }

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
            VssPhysicalFile physicalFile = new(physicalPath, Encoding);
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

        public VssDatabase(string path, Encoding encoding)
        {
            BasePath = path;
            Encoding = encoding;

            IniPath = Path.Combine(path, SourceSafeConstants.IniFile);
            IO.SimpleIniReader iniReader = new(IniPath);
            iniReader.Parse();

            DataPath = Path.Combine(path, iniReader.GetValue("Data_Path", "data"));

            string namesPath = Path.Combine(DataPath, "names.dat");
            mNameFile = new Physical.Files.Names.VssNamesDatFile(namesPath, encoding);
            mNameFile.ReadHeaderAndNames();

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
            return Path.Combine(Path.Combine(DataPath, physicalName.Substring(0, 1)), physicalName);
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
