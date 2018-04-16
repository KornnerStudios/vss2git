/* Copyright 2009 HPDI, LLC
 * 
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 * 
 *     http://www.apache.org/licenses/LICENSE-2.0
 * 
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

namespace Hpdi.HashLib
{
    /// <summary>
    /// 32-bit CRC hash function.
    /// </summary>
    /// <author>Trevor Robinson</author>
    public class Crc32 : Hash32
    {
        // Commonly used polynomials.
        public const uint IEEE = 0xEDB88320; // reversed

        private readonly uint[] table;
        private readonly uint initial;
        private readonly uint final;

        public Crc32(uint poly)
            : this(poly, 0, 0)
        {
        }

        public Crc32(uint poly, uint initial, uint final)
        {
            this.table = GenerateTable(poly);
            this.initial = initial;
            this.final = final;
        }

        public uint Compute(byte[] bytes)
        {
            return Compute(bytes, 0, bytes.Length);
        }

        public uint Compute(byte[] bytes, int offset, int limit)
        {
            var crc = initial;
            while (offset < limit)
            {
                crc = (uint)((crc >> 8) ^ table[(byte)(crc ^ bytes[offset++])]);
            }
            return (uint)(crc ^ final);
        }

        protected static uint[] GenerateTable(uint poly)
        {
            var table = new uint[256];
            for (int i = 0; i < table.Length; ++i)
            {
                var value = (uint)i;
                for (int j = 0; j < 8; ++j)
                {
                    var xor = (value & 1) != 0;
                    value >>= 1;
                    if (xor) value ^= poly;
                }
                table[i] = value;
            }
            return table;
        }
    }

	// http://create.stephan-brumme.com/crc32/#slicing-by-8-overview
	public class Crc32_FAST : Hash32
	{
		// Commonly used polynomials.
		public const uint IEEE = 0xEDB88320; // reversed

		private readonly uint[][] table;
		private readonly uint initial;
		private readonly uint final;

		public Crc32_FAST(uint poly)
			: this(poly, 0, 0)
		{
		}

		public Crc32_FAST(uint poly, uint initial, uint final)
		{
			this.table = GenerateTable(poly);
			this.initial = initial;
			this.final = final;
		}

		public uint Compute(byte[] bytes)
		{
			return Compute(bytes, 0, bytes.Length);
		}

		public uint Compute(byte[] bytes, int offset, int limit)
		{
			int len = limit - offset;

			var crc = initial;

			while (len >= sizeof(long))
			{
				uint one = System.BitConverter.ToUInt32(bytes, offset) ^ crc;
				offset += sizeof(uint);
				uint two = System.BitConverter.ToUInt32(bytes, offset);
				offset += sizeof(uint);
				crc =	table[7][ one      & 0xFF] ^
						table[6][(one>> 8) & 0xFF] ^
						table[5][(one>>16) & 0xFF] ^
						table[4][ one>>24        ] ^
						table[3][ two      & 0xFF] ^
						table[2][(two>> 8) & 0xFF] ^
						table[1][(two>>16) & 0xFF] ^
						table[0][ two>>24        ];
				len -= sizeof(long);
			}

			// process remaining bytes (can't be larger than 8)
			while (len > 0)
			{
				crc = (uint)((crc >> 8) ^ table[0][(byte)(crc ^ bytes[offset++])]);
				len--;
			}
			return (uint)(crc ^ final);
		}

		protected static uint[][] GenerateTable(uint poly)
		{
			var table = new uint[8][];
			for (int x = 0; x < table.Length; x++)
				table[x] = new uint[256];

			// generate CRCs for all single byte sequences
			for (int i = 0; i < table[0].Length; ++i)
			{
				var value = (uint)i;
				for (int j = 0; j < 8; ++j)
				{
					var xor = (value & 1) != 0;
					value >>= 1;
					if (xor) value ^= poly;
				}
				table[0][i] = value;
			}

			for (int i = 0; i <= 0xFF; i++)
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
	};
}
