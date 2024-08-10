
namespace SourceSafe.Physical.Files.Names
{
    public // #TODO temporary until VssDatabase is moved to SourceSafe.Core
    /*internal*/ sealed class VssNamesDatFile : VssRecordFileBase
    {
        internal /*public*/ NamesHeaderRecord Header { get; } = new();
        private readonly Dictionary<int, NamesRecord> mRecordsByFileOffset = [];

        public VssNamesDatFile(string filename, System.Text.Encoding encoding)
            : base(filename, encoding)
        {
        }

        public void ReadHeaderAndNames()
        {
            ReadRecord(Header);

            while (reader.Offset < Header.EofOffset)
            {
                NamesRecord record = new();
                ReadRecord(record);
                mRecordsByFileOffset[reader.Offset] = record;
            }
        }

        internal /*public*/ NamesRecord GetNameRecordByFileOffset(int fileOffset)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(fileOffset);
            ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(fileOffset, Header.EofOffset);

            if (!mRecordsByFileOffset.TryGetValue(fileOffset, out NamesRecord? record))
            {
                throw new ArgumentOutOfRangeException(nameof(fileOffset), fileOffset, "No record at the specified offset");
            }

            return record;
        }

        internal /*public*/ IEnumerable<NamesRecord> GetRecords()
        {
            return mRecordsByFileOffset.Values;
        }

        public string? TryAndGetProjectOrLongName(int fileOffset, bool isProject)
        {
            string? name = null;
            if (!mRecordsByFileOffset.TryGetValue(fileOffset, out NamesRecord? record))
            {
                NameKind nameKind = isProject ? NameKind.Project : NameKind.Long;
                name = record!.TryAndGetNameByKind(nameKind);
            }

            return name;
        }
    };
}
