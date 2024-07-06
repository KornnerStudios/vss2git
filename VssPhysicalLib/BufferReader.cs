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

using System;
using System.Text;
using Hpdi.HashLib;

namespace Hpdi.VssPhysicalLib
{
    /// <summary>
    /// Reads VSS data types from a byte buffer.
    /// </summary>
    /// <author>Trevor Robinson</author>
    public class BufferReader
    {
        public static bool ValidateAssumedToBeAllZerosAreAllZeros { get; set; }
            = true;

        private readonly Encoding encoding;
        private readonly byte[] data;
        private int offset;
        private readonly int limit;
        public string FileName { get; private set; }

        public BufferReader(Encoding encoding, byte[] data, string fileName)
            : this(encoding, data, 0, data.Length, fileName)
        {
        }

        public BufferReader(Encoding encoding, byte[] data, int offset, int limit, string fileName = null)
        {
            this.encoding = encoding;
            this.data = data;
            this.Offset = offset;
            this.limit = limit;
            FileName = fileName;
        }

        public int Offset
        {
            get => offset;
            set
            {
                if (value < 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(value), value, "Offset cannot be negative");
                }

                offset = value;
            }
        }

        public int Remaining => limit - Offset;

        public ushort Checksum16()
        {
            ushort sum = 0;
            for (int i = Offset; i < limit; ++i)
            {
                sum += data[i];
            }
            return sum;
        }

        private static readonly XorHash32To16 crc16 = new(new Crc32_FAST(Crc32_FAST.IEEE));

        public ushort Crc16()
        {
            return crc16.Compute(data, Offset, limit);
        }

        public ushort Crc16(int bytes)
        {
            CheckRead(bytes);
            return crc16.Compute(data, Offset, Offset + bytes);
        }

        public void Skip(int bytes)
        {
            CheckRead(bytes);
            Offset += bytes;
        }

        public void SkipUnknown(int bytes)
        {
            CheckRead(bytes);
            // #TODO: add logging
            Offset += bytes;
        }

        public void SkipAssumedToBeAllZeros(int bytes)
        {
            CheckRead(bytes);
            if (ValidateAssumedToBeAllZerosAreAllZeros)
            {
                int nonZeroBytesCount = 0;
                for (int i = 0; i < bytes; ++i)
                {
                    if (data[Offset + i] != 0)
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
            Offset += bytes;
        }

        public short ReadInt16()
        {
            CheckRead(2);
            return (short)(data[Offset++] | (data[Offset++] << 8));
        }

        public int ReadInt32()
        {
            CheckRead(4);
            return data[Offset++] | (data[Offset++] << 8) |
                (data[Offset++] << 16) | (data[Offset++] << 24);
        }

        private static readonly DateTime EPOCH =
            new(1970, 1, 1, 0, 0, 0, DateTimeKind.Local);

        public DateTime ReadDateTime()
        {
            return EPOCH + TimeSpan.FromSeconds(ReadInt32());
        }

        public string ReadSignature(int length)
        {
            CheckRead(length);
            StringBuilder buf = new(length);
            for (int i = 0; i < length; ++i)
            {
                buf.Append((char)data[Offset++]);
            }
            return buf.ToString();
        }

        public VssName ReadName()
        {
            CheckRead(2 + 34 + 4);
            return new VssName(ReadInt16(), ReadString(34), ReadInt32());
        }

        public string ReadString(int fieldSize)
        {
            CheckRead(fieldSize);

            int count = 0;
            for (int i = 0; i < fieldSize; ++i)
            {
                if (data[Offset + i] == 0) break;
                ++count;
            }

            string str = encoding.GetString(data, Offset, count);

            Offset += fieldSize;

            return str;
        }

        public string ReadByteString(int bytes)
        {
            CheckRead(bytes);
            string result = FormatBytes(bytes);
            Offset += bytes;
            return result;
        }

        public BufferReader ReadBytesIntoNewBufferReader(int bytes)
        {
            CheckRead(bytes);
            string newFileName = FileName;
            if (newFileName != null)
            {
                newFileName += $"__chunk[{Offset:X8}, {bytes:X4}]";
            }
            return new BufferReader(encoding, data, Offset, Offset += bytes, newFileName);
        }

        public ArraySegment<byte> GetBytes(int bytes)
        {
            CheckRead(bytes);
            var result = new ArraySegment<byte>(data, Offset, bytes);
            Offset += bytes;
            return result;
        }

        public string FormatBytes(int bytes)
        {
            int formatLimit = Math.Min(limit, Offset + bytes);
            StringBuilder buf = new((formatLimit - Offset) * 3);
            for (int i = Offset; i < formatLimit; ++i)
            {
                buf.AppendFormat("{0:X2} ", data[i]);
            }
            return buf.ToString();
        }

        public string FormatRemaining()
        {
            return FormatBytes(Remaining);
        }

        private void CheckRead(int bytes)
        {
            if (Offset + bytes > limit)
            {
                throw new EndOfBufferException(
                    $"Attempted read of {bytes} bytes with only {Remaining} bytes remaining in buffer for {FileName}");
            }
        }
    }
}
