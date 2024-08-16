
namespace SourceSafe.GitConversion.GitActions
{
    /// <summary>
    /// Represents a delete directory action.
    /// </summary>
    class DeleteDirectoryAction(
        string mPath,
        bool mContainsFiles
        ) : IGitAction
    {
        public bool Run(
            IO.SimpleLogger logger,
            IGitWrapper git,
            IGitStatistic stat)
        {
            logger.WriteLine("Deleting directory: {0}", mPath);

            if (mContainsFiles)
            {
                git.RemoveDir(mPath, true);
            }
            else
            {
                // git doesn't care about directories with no files
                if (Directory.Exists(mPath))
                {
                    Directory.Delete(mPath, true);
                }
            }

            return true;
        }
    };
}
