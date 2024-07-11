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
using System.Buffers.Binary;
using System.Text;

namespace Hpdi.VssPhysicalLib
{
    /// <summary>
    /// Reads VSS data types from a byte buffer.
    /// </summary>
    /// <author>Trevor Robinson</author>
    public sealed class BufferReader
    {
        public static bool ValidateAssumedToBeAllZerosAreAllZeros { get; set; }
            = true;

        private readonly Encoding mEncoding;
        private readonly ArraySegment<byte> mDataSegment;
        private int mOffset;
        public string FileName { get; private set; }

        public BufferReader(Encoding encoding, ArraySegment<byte> data, string fileName)
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
        // #TODO replace uses with RemainingSize
        public int Remaining => RemainingSize;

        // #REVIEW This is NOT thread-safe!
        private static readonly SourceSafe.Cryptography.Crc32ToXor16BitComputer mCrc16Computer = new(
            new SourceSafe.Cryptography.Crc32.Definition(initialValue: 0));

        public ushort Crc16(int bytes)
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(bytes, 0, nameof(bytes));

            CheckRead(bytes);
            ushort crc16 = mCrc16Computer.Compute(mDataSegment.AsSpan(Offset, bytes));
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
                buf.Append((char)mDataSegment[Offset++]);
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

        public BufferReader ReadBytesIntoNewBufferReader(int bytes)
        {
            CheckRead(bytes);
            string newFileName = FileName;
            if (newFileName != null)
            {
                newFileName += $"__chunk[{Offset:X8}, {bytes:X4}]";
            }
            var newBuffer = new BufferReader(mEncoding, mDataSegment.Slice(Offset, bytes), newFileName);
            mOffset += bytes;
            return newBuffer;
        }

        public ArraySegment<byte> GetBytes(int bytes)
        {
            CheckRead(bytes);
            var result = mDataSegment.Slice(Offset, bytes);
            mOffset += bytes;
            return result;
        }

        private void CheckRead(int bytes)
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(bytes, 0, nameof(bytes));

            if (Offset + bytes > Size)
            {
                throw new EndOfBufferException(
                    $"Attempted read of {bytes} bytes with only {Remaining} bytes remaining in buffer for {FileName}");
            }
        }
    }
}
