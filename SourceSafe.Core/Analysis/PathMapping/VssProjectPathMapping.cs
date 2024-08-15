
namespace SourceSafe.Analysis.PathMapping
{
    /// <summary>
    /// Represents the current state of a VSS project.
    /// </summary>
    public sealed class VssProjectPathMapping : VssItemPathMappingBase
    {
        private VssProjectPathMapping? mParentInfo;
        public VssProjectPathMapping? Parent
        {
            get => mParentInfo;
            set
            {
                if (mParentInfo != value)
                {

                    mParentInfo?.RemoveItem(this);
                    mParentInfo = value;
                    mParentInfo?.AddItem(this);
                }
            }
        }

        public bool IsRoot { get; set; }
        public string? OriginalVssPath { get; set; }
        public string? OriginalWorkDirPath { get; set; }

        public bool IsRooted
        {
            get
            {
                VssProjectPathMapping project = this;
                while (project.mParentInfo != null)
                {
                    project = project.mParentInfo;
                }
                return project.IsRoot;
            }
        }

        private readonly LinkedList<VssItemPathMappingBase> mItems = new();
        public IEnumerable<VssItemPathMappingBase> Items => mItems;

        public bool Destroyed { get; set; } = false;

        public VssProjectPathMapping(string physicalName, string logicalName)
            : base(physicalName, logicalName)
        {
        }

        public List<string>? GetWorkDirPath()
        {
            List<string>? path = null;

            if (IsRooted)
            {
                if (mParentInfo != null)
                {
                    path = mParentInfo.GetWorkDirPath();
                    path!.Add(LogicalName);
                }
                else
                {
                    path = [OriginalWorkDirPath];
                }
            }

            return path;
        }

        public List<string>? GetLogicalPath()
        {
            List<string>? path = null;

            if (IsRooted)
            {
                if (mParentInfo != null)
                {
                    path = mParentInfo.GetLogicalPath();
                    path!.Add(LogicalName);
                }
                else
                {
                    path = [OriginalVssPath];
                }
            }

            return path;
        }

        public bool IsSameOrSubproject(VssProjectPathMapping parentInfo)
        {
            VssProjectPathMapping? project = this;
            while (project != null)
            {
                if (project == parentInfo)
                {
                    return true;
                }
                project = project.mParentInfo;
            }
            return false;
        }

        public void AddItem(VssItemPathMappingBase item)
        {
            mItems.AddLast(item);
        }

        public void RemoveItem(VssItemPathMappingBase item)
        {
            mItems.Remove(item);
        }

        public bool ContainsLogicalName(string logicalName)
        {
            foreach (VssItemPathMappingBase item in mItems)
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
            var subprojects = new LinkedList<VssProjectPathMapping>();
            VssProjectPathMapping? project = this;
            while (project != null)
            {
                foreach (VssItemPathMappingBase item in project.mItems)
                {
                    if (item is VssProjectPathMapping subproject)
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

        public IEnumerable<VssFilePathMapping> GetAllFiles()
        {
            var subprojects = new LinkedList<VssProjectPathMapping>();
            VssProjectPathMapping? project = this;
            while (project != null)
            {
                foreach (VssItemPathMappingBase item in project.mItems)
                {
                    if (item is VssProjectPathMapping subproject)
                    {
                        subprojects.AddLast(subproject);
                    }
                    else
                    {
                        yield return (VssFilePathMapping)item;
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

        public IEnumerable<VssProjectPathMapping> GetAllProjects()
        {
            var subprojects = new LinkedList<VssProjectPathMapping>();
            VssProjectPathMapping? project = this;
            while (project != null)
            {
                foreach (VssItemPathMappingBase item in project.mItems)
                {
                    if (item is VssProjectPathMapping subproject)
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
    };
}
