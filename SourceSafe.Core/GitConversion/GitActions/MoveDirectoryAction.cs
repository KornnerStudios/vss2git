
namespace SourceSafe.GitConversion.GitActions
{
    /// <summary>
    /// Represents a git move directory action.
    /// </summary>
    sealed class MoveDirectoryAction(
        string sourcePath,
        string targetPath,
        bool mContainsFiles)
        : MoveActionBase(sourcePath, targetPath)
    {
        public override bool Run(
            IO.SimpleLogger logger,
            IGitWrapper git,
            IGitStatistic stat)
        {
            logger.WriteLine("Moving directory: {0} to {1}", sourcePath, targetPath);

            if (Directory.Exists(sourcePath))
            {
                RenameDelegate? renameDelegate = null;

                if (mContainsFiles)
                {
                    renameDelegate = git.MoveDir;
                }
                else
                {
                    renameDelegate = Directory.Move;
                }

                CaseSensitiveRename(sourcePath, targetPath, renameDelegate);
            }
            else
            {
                logger.WriteLine("NOTE: Skipping rename because {0} does not exist", sourcePath);
            }

            return true;
        }
    };
}
