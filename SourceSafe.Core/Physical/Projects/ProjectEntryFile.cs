
namespace SourceSafe.Physical.Projects
{
    /// <summary>
    /// Represents a file containing VSS project entry records.
    /// </summary>
    public sealed class ProjectEntryFile : Files.VssRecordFileBase
    {
        public ProjectEntryFile(
            Logical.VssDatabase vssDatabase,
            string fileName)
            : base(vssDatabase, fileName)
        {
        }

        public ProjectEntryRecord? GetFirstEntry()
        {
            mReader.Offset = 0;
            return GetNextEntry();
        }

        public ProjectEntryRecord? GetNextEntry()
        {
            ProjectEntryRecord record = new();
            return ReadNextRecord(record) ? record : null;
        }
    };
}
