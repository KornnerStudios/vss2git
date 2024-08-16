
namespace SourceSafe.GitConversion.GitActions
{
    /// <summary>
    /// Represents a create directory action.
    /// </summary>
    sealed class CreateDirectoryAction(
        string mPath
        ) : IGitAction
    {
        public bool Run(
            IO.SimpleLogger logger,
            IGitWrapper git,
            IGitStatistic stat)
        {
            logger.WriteLine("Creating directory: {0}", mPath);

            Directory.CreateDirectory(mPath);

            return true;
        }
    };
}
