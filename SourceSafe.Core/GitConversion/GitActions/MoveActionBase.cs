
namespace SourceSafe.GitConversion.GitActions
{
    /// <summary>
    /// Represents a generic git move action.
    /// </summary>
    abstract class MoveActionBase : IGitAction
    {
        protected readonly string sourcePath;
        protected readonly string targetPath;

        public MoveActionBase(string sourcePath, string targetPath)
        {
            this.sourcePath = sourcePath;
            this.targetPath = targetPath;
        }

        public abstract bool Run(
            IO.SimpleLogger logger,
            IGitWrapper git,
            IGitStatistic stat);

        protected delegate void RenameDelegate(
            string sourcePath,
            string destinationPath);

        protected static void CaseSensitiveRename(
            string sourcePath,
            string destinationPath,
            RenameDelegate renamer)
        {
            if (sourcePath.Equals(destinationPath, StringComparison.OrdinalIgnoreCase))
            {
                // workaround for case-only renames on case-insensitive file systems:

                string? sourceDir = Path.GetDirectoryName(sourcePath);
                string sourceFile = Path.GetFileName(sourcePath);
                string? destinationDir = Path.GetDirectoryName(destinationPath);
                string destinationFile = Path.GetFileName(destinationPath);

                if (sourceDir != destinationDir)
                {
                    // recursively rename containing directories that differ in case
                    CaseSensitiveRename(sourceDir!, destinationDir!, renamer);

                    // fix up source path based on renamed directory
                    sourcePath = Path.Combine(destinationDir!, sourceFile);
                }

                if (sourceFile != destinationFile)
                {
                    // use temporary filename to rename files that differ in case
                    string tempPath = sourcePath + ".mvtmp";
                    CaseSensitiveRename(sourcePath, tempPath, renamer);
                    CaseSensitiveRename(tempPath, destinationPath, renamer);
                }
            }
            else
            {
                renamer(sourcePath, destinationPath);
            }
        }
    };
}
