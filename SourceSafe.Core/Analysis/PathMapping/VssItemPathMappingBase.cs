
namespace SourceSafe.Analysis.PathMapping
{
    /// <summary>
    /// Base class for representing VSS items.
    /// </summary>
    public abstract class VssItemPathMappingBase
    {
        public string PhysicalName { get; }
        public string LogicalName { get; set; }

        protected VssItemPathMappingBase(string physicalName, string logicalName)
        {
            PhysicalName = physicalName;
            LogicalName = logicalName;
        }
    };
}
