
namespace SourceSafe.GitConversion.GitActions
{
    /// <summary>
    /// Represents a branch based git action.
    /// </summary>
    abstract class BranchActionBase : IGitAction
    {
        public abstract bool Run(
            IO.SimpleLogger logger,
            IGitWrapper git,
            IGitStatistic stat);
    };
}
