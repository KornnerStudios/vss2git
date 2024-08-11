
namespace SourceSafe.Physical.Projects
{
    /// <summary>
    /// Represents a file containing VSS project entry records.
    /// </summary>
    public sealed class ProjectEntryFile : Files.VssRecordFileBase
    {
        public ProjectEntryFile(string filename, System.Text.Encoding encoding)
            : base(filename, encoding)
        {
        }

        public ProjectEntryRecord? GetFirstEntry()
        {
            reader.Offset = 0;
            return GetNextEntry();
        }

        public ProjectEntryRecord? GetNextEntry()
        {
            ProjectEntryRecord record = new();
            return ReadNextRecord(record) ? record : null;
        }
    };
}
