
namespace SourceSafe.Analysis.PathMapping
{
    /// <summary>
    /// Represents the current state of a VSS file.
    /// </summary>
    public sealed class VssFilePathMapping : VssItemPathMappingBase
    {
        private readonly Dictionary<VssProjectPathMapping, VssPinInfo> mProjectToPinInfo = [];
        public IEnumerable<KeyValuePair<VssProjectPathMapping, VssPinInfo>> ProjectAndPinPairs => mProjectToPinInfo;

        private readonly Dictionary<VssProjectPathMapping, bool> mProjectToDestroyedFlag = [];
        public IEnumerable<KeyValuePair<VssProjectPathMapping, bool>> ProjectToDestroyedFlag => mProjectToDestroyedFlag;

        public int Version { get; set; } = 1;

        public VssFilePathMapping(
            string physicalName,
            string logicalName)
            : base(physicalName, logicalName)
        {
        }

        public void AddProject(
            VssProjectPathMapping project,
            bool destroyed)
        {
            mProjectToPinInfo[project] = new VssPinInfo(false, -1, destroyed);
        }

        public void AddProject(
            VssProjectPathMapping project,
            int revision,
            bool destroyed)
        {
            mProjectToPinInfo[project] = new VssPinInfo(true, revision, destroyed);
        }

        public void RemoveProject(
            VssProjectPathMapping project)
        {
            mProjectToPinInfo.Remove(project);
        }

        public VssPinInfo? GetProjectPin(
            VssProjectPathMapping project)
        {
            if (!mProjectToPinInfo.TryGetValue(project, out VssPinInfo pin))
            {
                return null;
            }
            return pin;
        }
    };
}
