
namespace SourceSafe.Physical.Files.Names
{
    /// <summary>
    /// VSS record containing the logical names of an object in particular contexts.
    /// </summary>
    internal class NamesRecord : Records.VssRecordBase
    {
        record struct NameEntry
        {
            // This is arbitrary, but it's the maximum length of a path in Windows
            public const int MAX_NAME_LENGTH = 260;

            public NameKind Kind;
            public short NameOffset;
            public string Name;
        };

        public const string SIGNATURE = "SN";

        public override string Signature => SIGNATURE;

        private NameEntry[] mEntries = [];

        public string? TryAndGetNameByKind(NameKind kind)
        {
            string? name = null;
            foreach (NameEntry entry in mEntries)
            {
                if (entry.Kind == kind)
                {
                    name = entry.Name;
                    break;
                }
            }

            return name;
        }

        public override void Read(IO.VssBufferReader reader, Records.RecordHeader header)
        {
            base.Read(reader, header);

            int entriesCount = reader.ReadInt16();
            if (entriesCount < 0)
            {
                throw new InvalidDataException($"Invalid entries count: {entriesCount}");
            }
            reader.SkipKnownJunk(sizeof(short));

            mEntries = new NameEntry[entriesCount];
            for (int x = 0 ; x < entriesCount; x++)
            {
                mEntries[x].Kind = (NameKind)reader.ReadInt16();
                mEntries[x].NameOffset = reader.ReadInt16();
            }

            //int baseOffset = reader.Offset;
            for (int x = 0; x < entriesCount; x++)
            {
                mEntries[x].Name = reader.ReadCString(NameEntry.MAX_NAME_LENGTH);
            }
        }

        public override void Dump(Analysis.AnalysisTextDumper textDumper)
        {
            for (int i = 0; i < mEntries.Length; ++i)
            {
                textDumper.WriteLine($"{mEntries[i].Kind} name: {mEntries[i].Name}");
            }
        }
    };
}
