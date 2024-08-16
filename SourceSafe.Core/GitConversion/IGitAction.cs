
namespace SourceSafe.GitConversion
{
    /// <summary>
    /// Represents a generic interface for replaying git action.
    /// </summary>
    interface IGitAction
    {
        bool Run(
            IO.SimpleLogger logger,
            IGitWrapper git,
            IGitStatistic stat);
    };
}
