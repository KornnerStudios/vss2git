using System.Diagnostics;

namespace SourceSafe.Cryptography
{
    partial class Crc32
    {
        // "Fast" based on http://create.stephan-brumme.com/crc32/#slicing-by-8-overview
        public sealed class Definition
        {
            public uint Polynomial { get; }
            public uint[][] FastCrcTable { get; }
            public uint InitialValue { get; }
            public uint XorIn { get; }
            public uint XorOut { get; }

            static uint[][] BuildFastCrcTable(uint polynomial)
            {
                uint[][] table = new uint[8][];
                for (int x = 0; x < table.Length; x++)
                {
                    table[x] = new uint[kCrcTableSize];
                }

                // generate CRCs for all single byte sequences
                for (int i = 0; i < table[0].Length; ++i)
                {
                    uint value = (uint)i;
                    for (int j = 0; j < 8; ++j)
                    {
                        bool xor = (value & 1) != 0;
                        value >>= 1;
                        if (xor) value ^= polynomial;
                    }
                    table[0][i] = value;
                }

                for (int i = 0; i < kCrcTableSize; i++)
                {
                    // for Slicing-by-4 and Slicing-by-8
                    table[1][i] = (table[0][i] >> 8) ^ table[0][table[0][i] & 0xFF];
                    table[2][i] = (table[1][i] >> 8) ^ table[0][table[1][i] & 0xFF];
                    table[3][i] = (table[2][i] >> 8) ^ table[0][table[2][i] & 0xFF];
                    // only Slicing-by-8
                    table[4][i] = (table[3][i] >> 8) ^ table[0][table[3][i] & 0xFF];
                    table[5][i] = (table[4][i] >> 8) ^ table[0][table[4][i] & 0xFF];
                    table[6][i] = (table[5][i] >> 8) ^ table[0][table[5][i] & 0xFF];
                    table[7][i] = (table[6][i] >> 8) ^ table[0][table[6][i] & 0xFF];
                }
                return table;
            }

            public Definition(
                uint polynomial = kDefaultPolynomial,
                uint initialValue = uint.MaxValue,
                uint xorIn = 0,
                uint xorOut = 0)
            {
                Polynomial = polynomial;
                InitialValue = initialValue;
                XorIn = xorIn;
                XorOut = xorOut;

                FastCrcTable = BuildFastCrcTable(Polynomial);
            }

            internal uint HashCoreFast(uint crc, ReadOnlySpan<byte> source)
            {
                int offset = 0;
                int len = source.Length;

                while (len >= sizeof(long))
                {
                    uint one = BitConverter.ToUInt32(source[offset..]) ^ crc;
                    offset += sizeof(uint);
                    uint two = BitConverter.ToUInt32(source[offset..]);
                    offset += sizeof(uint);
                    crc =	FastCrcTable[7][ one      & 0xFF] ^
                            FastCrcTable[6][(one>> 8) & 0xFF] ^
                            FastCrcTable[5][(one>>16) & 0xFF] ^
                            FastCrcTable[4][ one>>24        ] ^
                            FastCrcTable[3][ two      & 0xFF] ^
                            FastCrcTable[2][(two>> 8) & 0xFF] ^
                            FastCrcTable[1][(two>>16) & 0xFF] ^
                            FastCrcTable[0][ two>>24        ];
                    len -= sizeof(long);
                }

                // process remaining bytes (can't be larger than 8)
                while (len > 0)
                {
                    crc = ((crc >> 8) ^ FastCrcTable[0][(byte)(crc ^ source[offset++])]);
                    len--;
                }

                return crc;
            }
        };
    };
}
