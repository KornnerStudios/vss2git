using System.Text.Json.Serialization;

namespace SourceSafe.Logical
{
    public partial class VssDatabaseConfig
    {
        /// <summary>
        /// Keys should be the relative path, in ALL UPPERCASE.
        /// Values should be the new relative path.
        /// </summary>
        /// <remarks>
        /// This can be useful for dealing with corrupted files
        /// where you need to remap the local corrupted file path
        /// to a (manually) fixed file path.
        /// </remarks>
        public Dictionary<string, string>? FileRemapping { get; set; }

        public void ForcePathCollectionsToExpectedFormat()
        {
            FileRemapping =
                CollectionUtils.ConvertForwardSlashesToBackSlashAndToUpper(FileRemapping, alsoConvertValues: false);

            ConfigFiles.ForcePathCollectionsToExpectedFormat();
            ConfigRecords.ForcePathCollectionsToExpectedFormat();
        }
    };
}
