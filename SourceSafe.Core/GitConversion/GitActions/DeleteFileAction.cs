
namespace SourceSafe.GitConversion.GitActions
{
    /// <summary>
    /// Represents a delete file action.
    /// </summary>
    sealed class DeleteFileAction(
        string mPath
        ) : IGitAction
    {
        public bool Run(
            IO.SimpleLogger logger,
            IGitWrapper git,
            IGitStatistic stat)
        {
            logger.WriteLine("Deleting file: {0}", mPath);

            if (File.Exists(mPath))
            {
                git.RemoveFile(mPath);
            }

            return true;
        }
    };
}
