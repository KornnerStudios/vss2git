
namespace SourceSafe.Logical
{
    /// <summary>
    /// Base class for VSS project actions that target a particular item.
    /// </summary>
    public abstract class VssNamedActionBase : VssActionBase
    {
        public VssItemName Name { get; init; }

        protected VssNamedActionBase(VssItemName name)
        {
            Name = name;
        }
    };
}
