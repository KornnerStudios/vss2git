namespace SourceSafe.Logical.Actions
{
    /// <summary>
    /// Base class for VSS revision action descriptions.
    /// </summary>
    public abstract class VssActionBase
    {
        public abstract VssActionType Type { get; }
    };
}
