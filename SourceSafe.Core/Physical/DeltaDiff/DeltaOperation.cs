using System.Text;

namespace SourceSafe.Physical.DeltaDiff
{
    /// <summary>
    /// Represents a single delta operation for a file revision.
    /// </summary>
    public sealed class DeltaOperation
    {
        ArraySegment<byte> data; // WriteLog only

        public DeltaCommand Command { get; private set; }
        public int Offset { get; private set; }
        public int Length { get; private set; }
        public ArraySegment<byte> Data { get { return data; } }

        public ArraySegment<byte> GetWriteLogDataSlice(int offset, int count)
        {
            if (Command != DeltaCommand.WriteLog)
            {
                throw new InvalidOperationException("GetWriteLogDataSlice called on non-WriteLog operation");
            }

            ArraySegment<byte> slice = Data.Slice(offset, count);
            return slice;
        }

        public static DeltaOperation WriteLog(byte[] data, int offset, int length)
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(offset, 0, nameof(offset));
            ArgumentOutOfRangeException.ThrowIfLessThan(length, 0, nameof(length));

            if (data == null)
            {
                if (length == 0)
                {
                    data = Array.Empty<byte>();
                }
                else
                {
                    throw new ArgumentNullException(nameof(data));
                }
            }

            var result = new DeltaOperation
            {
                Command = DeltaCommand.WriteLog,
                Length = length,
                data = new ArraySegment<byte>(data, offset, length),
            };
            return result;
        }

        public static DeltaOperation WriteSuccessor(int offset, int length)
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(offset, 0, nameof(offset));
            ArgumentOutOfRangeException.ThrowIfLessThan(length, 0, nameof(length));

            var result = new DeltaOperation
            {
                Command = DeltaCommand.WriteSuccessor,
                Offset = offset,
                Length = length,
            };
            return result;
        }

        public void Read(IO.VssBufferReader reader)
        {
            Command = (DeltaCommand)reader.ReadInt16();
            // Note in ApplyDifferenceData: "Next 16 bits is junk.  Ignore it."
            reader.SkipKnownJunk(2);
            Offset = reader.ReadInt32();
            Length = reader.ReadInt32();
            if (Command == DeltaCommand.WriteLog)
            {
                data = reader.GetBytes(Length);
            }
        }

        // #TODO add JSON flag to control this behavior
        public static bool IncludeDataBytesInDump { get; set; } = false;
        public void Dump(Analysis.AnalysisTextDumper textDumper)
        {
            const int MAX_DATA_DUMP = 40;

            textDumper.WriteIndent();
            textDumper.Write($"Offset={Offset:X8}, Length={Length:X4}, {Command}");
            if (IncludeDataBytesInDump && data.Array != null)
            {
                int dumpLength = data.Count;
                bool truncated = false;
                if (dumpLength > MAX_DATA_DUMP)
                {
                    dumpLength = MAX_DATA_DUMP;
                    truncated = true;
                }

                StringBuilder buf = new(dumpLength);
                for (int i = 0; i < dumpLength; ++i)
                {
                    byte b = data.Array[data.Offset + i];
                    buf.Append(b >= 0x20 && b <= 0x7E ? (char)b : '.');
                }
                textDumper.Write($", Data: {buf}{(truncated ? "..." : "")}");
            }
            textDumper.WriteLine();
        }
    };
}
