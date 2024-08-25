
namespace SourceSafe.Physical.Files.Names
{
    internal sealed class VssNamesDatFile : VssRecordFileBase
    {
        public NamesHeaderRecord Header { get; } = new();
        private readonly Dictionary<int, NamesRecord> mRecordsByFileOffset = [];

        public VssNamesDatFile(
            Logical.VssDatabase vssDatabase,
            string fileName)
            : base(vssDatabase, fileName)
        {
        }

        public void ReadHeaderAndNames()
        {
            ReadRecord(Header);

            while (mReader.Offset < Header.EofOffset)
            {
                int recordOffset = mReader.Offset;
                NamesRecord record = new();
                ReadRecord(record);
                mRecordsByFileOffset[recordOffset] = record;
            }
        }

        public NamesRecord GetNameRecordByFileOffset(int fileOffset)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(fileOffset);
            ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(fileOffset, Header.EofOffset);

            if (!mRecordsByFileOffset.TryGetValue(fileOffset, out NamesRecord? record))
            {
                throw new ArgumentOutOfRangeException(nameof(fileOffset), fileOffset, "No record at the specified offset");
            }

            return record;
        }

        public IEnumerable<NamesRecord> GetRecords()
        {
            return mRecordsByFileOffset.Values;
        }

        public string? TryAndGetProjectOrLongName(int fileOffset, bool isProject)
        {
            string? name = null;
            if (mRecordsByFileOffset.TryGetValue(fileOffset, out NamesRecord? record))
            {
                NameKind nameKind = isProject ? NameKind.Project : NameKind.Long;
                name = record.TryAndGetNameByKind(nameKind);
            }

            return name;
        }
    };
}
