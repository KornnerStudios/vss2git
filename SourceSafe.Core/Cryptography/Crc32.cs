using HashAlgorithm = System.Security.Cryptography.HashAlgorithm;

namespace SourceSafe.Cryptography
{
    public static partial class Crc32
    {
        public const int kCrcTableSize = 256;
        public const uint kDefaultPolynomial = 0xEDB88320; // 0x04C11DB7 nets the same results
        public static Definition DefaultDefinition { get; } = new Definition();
    };

    public sealed class CrcHash32
        : HashAlgorithm
    {
        #region Registeration
        public const string kAlgorithmName = "KSoft.Security.Cryptography.CrcHash32";

        public new static CrcHash32 Create(string algName)
        {
            CrcHash32 result = (CrcHash32?)System.Security.Cryptography.CryptoConfig.CreateFromName(algName) ?? new CrcHash32();
            return result;
        }
        public new static CrcHash32 Create()
        {
            return Create(kAlgorithmName);
        }

        static CrcHash32()
        {
            System.Security.Cryptography.CryptoConfig.AddAlgorithm(typeof(CrcHash32), kAlgorithmName);
        }
        #endregion

        readonly Crc32.Definition mDefinition;
        readonly byte[] mHashBytes;
        public uint Hash32 { get; private set; }

        public byte[] InternalHashBytes => mHashBytes;
        public Crc32.Definition Definition => mDefinition;

        public CrcHash32()
            : this(Crc32.DefaultDefinition)
        {
        }

        public CrcHash32(Crc32.Definition definition)
        {
            ArgumentNullException.ThrowIfNull(definition);

            base.HashSizeValue = 32/*Bits.kInt32BitCount*/;

            mDefinition = definition;
            mHashBytes = new byte[sizeof(uint)];

            // NOTE: the original KSoft implementation did not call Initialize in the constructor,
            // which is a bug because this would not seed with InitialValue or apply XorIn.
            // Things just happened to work because most uses in KSoft called Initialize after construction.
            Initialize();
        }

        public override void Initialize()
        {
            Array.Clear(mHashBytes, 0, mHashBytes.Length);
            Hash32 = mDefinition.InitialValue;

            Hash32 ^= mDefinition.XorIn;
        }

        /// <summary>Performs the hash algorithm on the data provided.</summary>
        /// <param name="array">The array containing the data.</param>
        /// <param name="startIndex">The position in the array to begin reading from.</param>
        /// <param name="count">How many bytes in the array to read.</param>
        protected override void HashCore(byte[] array, int startIndex, int count)
        {
            var span = new ReadOnlySpan<byte>(array, startIndex, count);
            Hash32 = mDefinition.HashCoreFast(Hash32, span);
        }

        protected override void HashCore(ReadOnlySpan<byte> source)
        {
            Hash32 = mDefinition.HashCoreFast(Hash32, source);
        }

        /// <summary>Performs any final activities required by the hash algorithm.</summary>
        /// <returns>The final hash value.</returns>
        protected override byte[] HashFinal()
        {
            Hash32 ^= mDefinition.XorOut;
            BitConverter.TryWriteBytes(mHashBytes, Hash32);
            return mHashBytes;
        }
    };
}
