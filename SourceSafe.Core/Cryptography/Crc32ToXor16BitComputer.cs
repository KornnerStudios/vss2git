
namespace SourceSafe.Cryptography
{
    public sealed class Crc32ToXor16BitComputer
    {
        private readonly CrcHash32 mCrcHash32;
        private readonly byte[] mHashBytes = new byte[sizeof(uint)];

        public Crc32ToXor16BitComputer(Crc32.Definition definition)
        {
            mCrcHash32 = new(definition);
        }

        public ushort Compute(ReadOnlySpan<byte> bytes)
        {
            mCrcHash32.Initialize();
            mCrcHash32.TryComputeHash(bytes, mHashBytes, out int bytesWritten);
            uint hash32 = BitConverter.ToUInt32(mHashBytes);
            return (ushort)(hash32 ^ (hash32 >> 16));
        }

        public ushort Compute(byte[] data)
        {
            ArgumentNullException.ThrowIfNull(data);
            return Compute(data, 0, data.Length);
        }

        public ushort Compute(byte[] data, int offset, int count)
        {
            ArgumentNullException.ThrowIfNull(data);
            ArgumentOutOfRangeException.ThrowIfLessThan(offset, 0);
            ArgumentOutOfRangeException.ThrowIfLessThan(count, 0);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(offset + count, data.Length);

            mCrcHash32.Initialize();
            byte[] hashBytes = mCrcHash32.ComputeHash(data, offset, count);
            uint hash32 = BitConverter.ToUInt32(hashBytes);
            return (ushort)(hash32 ^ (hash32 >> 16));
        }
    };
}
