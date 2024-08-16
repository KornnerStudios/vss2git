using SourceSafe.Logical;
using SourceSafe.Logical.Items;

namespace SourceSafe.GitConversion.GitActions
{
    /// <summary>
    /// Represents a VSS-revision-to-git file writer action.
    /// </summary>
    sealed class WriteFileAction : IGitAction
    {
        public static bool SleepThreadBeforeSetLastWriteTimeUtc { get; set; } = true;

        private readonly VssDatabase mDatabase;
        private readonly string mPhysicalName;
        private readonly int mVersion;
        private readonly string mDestinationPath;

        public WriteFileAction(
            VssDatabase database,
            string physicalName,
            int version,
            string destinationPath)
        {
            mDatabase = database;
            mPhysicalName = physicalName;
            mVersion = version;
            mDestinationPath = destinationPath;
        }

        public bool Run(
            IO.SimpleLogger logger,
            IGitWrapper git,
            IGitStatistic stat)
        {
            logger.WriteLine($"Writing file: {mDestinationPath} ({mPhysicalName})@{mVersion}");

            VssFileItem item;
            VssFileItemRevision revision;
            Stream? contents = null;
            try
            {
                item = (VssFileItem)mDatabase.GetItemByPhysicalName(mPhysicalName);
                revision = item.GetRevision(mVersion);
                contents = revision.GetContents();
            }
            catch (Exception e)
            {
                // log an error for missing data files or versions, but keep processing
                string message = Exceptions.ExceptionFormatter.Format(e);
                logger.WriteLine($"ERROR: {message}");
                logger.WriteLine(e);
                return false;
            }

            if (contents != null)
            {
                // propagate exceptions here (e.g. disk full) to abort/retry/ignore
                using (contents)
                {
                    WriteStream(contents, mDestinationPath);
                }
            }

            // try to use the first revision (for this branch) as the create time,
            // since the item creation time doesn't seem to be meaningful
            DateTime createDateTime = item.Created;
            using (System.Collections.Generic.IEnumerator<VssFileItemRevision> revEnum = item.Revisions.GetEnumerator())
            {
                if (revEnum.MoveNext())
                {
                    createDateTime = revEnum.Current.DateTime;
                }
            }

            try
            {
                DateTime createDateTimeUtc = createDateTime.ConvertAmbiguousTimeToUtc(logger);
                DateTime revisionDateTimeUtc = revision.DateTime.ConvertAmbiguousTimeToUtc(logger);

                // set file creation and update timestamps
                File.SetCreationTimeUtc(mDestinationPath, createDateTimeUtc);
                if (SleepThreadBeforeSetLastWriteTimeUtc)
                {
                    // I've had one failure case for SetLastWriteTimeUtc because SetCreationTimeUtc had not yet completed.
                    // This was in 2018 on a HDD, perhaps less of a concern on SSDs?
                    System.Threading.Thread.Sleep(343);
                }
                File.SetLastWriteTimeUtc(mDestinationPath, revisionDateTimeUtc);
            }
            catch (Exception e)
            {
                // log an error for missing data files or versions, but keep processing
                string message = Exceptions.ExceptionFormatter.Format(e);
                logger.WriteLine($"ERROR: {message}");
                logger.WriteLine(e);
                return false;
            }

            git.Add(mDestinationPath);

            return true;
        }

        private static void WriteStream(
            Stream inputStream,
            string path)
        {
            string? directoryName = Path.GetDirectoryName(path);
            Directory.CreateDirectory(directoryName!);

            using (var outputStream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                inputStream.CopyTo(outputStream);
            }
        }
    };
}
