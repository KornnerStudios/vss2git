using System.Buffers.Binary;
using System.Text;

namespace SourceSafe.IO
{
    /// <summary>
    /// Reads VSS data types from a byte buffer.
    /// </summary>
    public sealed class VssBufferReader
    {
        public static bool ValidateAssumedToBeAllZerosAreAllZerosDefault { get; set; }
            = true;

        private static readonly Cryptography.Crc32.Definition mCrc32Definition = new(
            initialValue: 0);
        [ThreadStatic]
        private static Cryptography.Crc32ToXor16BitComputer? mCrc16Computer;
        private static Cryptography.Crc32ToXor16BitComputer GetCrc16Computer() => mCrc16Computer ??= new(mCrc32Definition);

        private readonly Encoding mEncoding;
        private readonly ArraySegment<byte> mDataSegment;
        private int mOffset;
        public string FileName { get; private set; }
        public bool ValidateAssumedToBeAllZerosAreAllZeros { get; set; }
            = ValidateAssumedToBeAllZerosAreAllZerosDefault;

        public VssBufferReader(Encoding encoding, ArraySegment<byte> data, string fileName)
        {
            mDataSegment = data;

            mEncoding = encoding;
            FileName = fileName;
        }

        public int Offset
        {
            get => mOffset;
            set
            {
                if (value < 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(value), value, "Offset cannot be negative");
                }

                mOffset = value;
            }
        }

        public int Size => mDataSegment.Count;
        public int RemainingSize => Size - Offset;

        public ushort Crc16(int bytes)
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(bytes, 0, nameof(bytes));

            CheckRead(bytes);
            ushort crc16 = GetCrc16Computer().Compute(mDataSegment.AsSpan(Offset, bytes));
            return crc16;
        }

        public void Skip(int bytes)
        {
            CheckRead(bytes);
            mOffset += bytes;
        }

        public void SkipKnownJunk(int bytes) => Skip(bytes);

        public void SkipUnknown(int bytes)
        {
            CheckRead(bytes);
            // #TODO: add logging
            mOffset += bytes;
        }

        public void SkipAssumedToBeAllZeros(int bytes)
        {
            CheckRead(bytes);
            if (ValidateAssumedToBeAllZerosAreAllZeros)
            {
                int nonZeroBytesCount = 0;
                for (int i = 0; i < bytes; ++i)
                {
                    if (mDataSegment[Offset + i] != 0)
                    {
                        nonZeroBytesCount++;
                    }
                }

                if (nonZeroBytesCount > 0)
                {
                    throw new InvalidOperationException(
                        $"Expected {bytes} bytes to be all zeros, but {nonZeroBytesCount} were not at offset {Offset:X8} in {FileName}");
                }
            }
            mOffset += bytes;
        }

        public short ReadInt16()
        {
            CheckRead(sizeof(short));
            short value = BinaryPrimitives.ReadInt16LittleEndian(mDataSegment.AsSpan(Offset));
            mOffset += sizeof(short);
            return value;
        }

        public int ReadInt32()
        {
            CheckRead(sizeof(int));
            int value = BinaryPrimitives.ReadInt32LittleEndian(mDataSegment.AsSpan(Offset));
            mOffset += sizeof(int);
            return value;
        }

        public DateTime ReadDateTime()
        {
            return SourceSafeConstants.UnixLocalTimeEpoch + TimeSpan.FromSeconds(ReadInt32());
        }

        public string ReadSignature(int length)
        {
            CheckRead(length);
            StringBuilder buf = new(length);
            for (int i = 0; i < length; ++i)
            {
                buf.Append((char)mDataSegment[Offset++]);
            }
            return buf.ToString();
        }

        public Physical.VssName ReadName()
        {
            CheckRead(Physical.VssName.Length);
            return new Physical.VssName(ReadInt16(), ReadString(Physical.VssName.ShortNameLength), ReadInt32());
        }

        public string ReadString(int fieldSize)
        {
            CheckRead(fieldSize);

            int count = 0;
            for (int i = 0; i < fieldSize; ++i)
            {
                if (mDataSegment[Offset + i] == 0)
                {
                    break;
                }
                ++count;
            }

            string str = mEncoding.GetString(mDataSegment.AsSpan(Offset, count));

            mOffset += fieldSize;

            return str;
        }

        public VssBufferReader ReadBytesIntoNewBufferReader(int bytes)
        {
            CheckRead(bytes);

            string newFileName = FileName;
            newFileName += $"__chunk[{Offset:X8}, {bytes:X4}]";

            var newBuffer = new VssBufferReader(mEncoding, mDataSegment.Slice(Offset, bytes), newFileName);
            mOffset += bytes;
            return newBuffer;
        }

        public ArraySegment<byte> GetBytes(int bytes)
        {
            CheckRead(bytes);
            ArraySegment<byte> result = mDataSegment.Slice(Offset, bytes);
            mOffset += bytes;
            return result;
        }

        private void CheckRead(int bytes)
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(bytes, 0, nameof(bytes));

            if (Offset + bytes > Size)
            {
                throw new EndOfBufferException(
                    $"Attempted read of {bytes} bytes with only {RemainingSize} bytes remaining in buffer for {FileName}");
            }
        }
    };
}
