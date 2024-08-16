
namespace SourceSafe.GitConversion.GitActions
{
    /// <summary>
    /// Represents a git move file action.
    /// </summary>
    sealed class MoveFileAction(
        string sourcePath,
        string targetPath)
        : MoveActionBase(sourcePath, targetPath)
    {
        public override bool Run(
            IO.SimpleLogger logger,
            IGitWrapper git,
            IGitStatistic stat)
        {
            logger.WriteLine("Moving file: {0} to {1}", sourcePath, targetPath);

            if (File.Exists(sourcePath))
            {
                CaseSensitiveRename(sourcePath, targetPath, git.MoveFile);
            }
            else
            {
                logger.WriteLine("NOTE: Skipping rename because {0} does not exist", sourcePath);
            }

            return true;
        }
    };
}
