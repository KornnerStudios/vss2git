﻿/* Copyright 2009 HPDI, LLC
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

using System.IO;

namespace Hpdi.VssPhysicalLib
{
    /// <summary>
    /// Represents the header of a VSS record.
    /// </summary>
    /// <author>Trevor Robinson</author>
    public class RecordHeader
    {
        public const int LENGTH = 8;

		public static bool IgnoreCrcErrors = false;

        int offset;
        int length;
        string signature;
        ushort fileCrc;
        ushort actualCrc;

        public int Offset { get { return offset; } }
        public int Length { get { return length; } }
        public string Signature { get { return signature; } }
        public ushort FileCrc { get { return fileCrc; } }
        public ushort ActualCrc { get { return actualCrc; } }
        public bool IsCrcValid { get { return fileCrc == actualCrc; } }

        public void CheckSignature(string expected)
        {
            if (signature != expected)
            {
                throw new RecordNotFoundException(string.Format(
                    "Unexpected record signature: expected={0}, actual={1}",
                    expected, signature));
            }
        }

        public void CheckCrc(string fileName = null)
        {
            if (!IsCrcValid)
            {
				if (IgnoreCrcErrors)
				{
					System.Diagnostics.Debug.WriteLine(
						"CRC error in {0} record: expected={1}, actual={2} in {3}",
						signature, fileCrc, actualCrc, fileName);
					return;
				}

				throw new RecordCrcException(this, string.Format(
                    "CRC error in {0} record: expected={1}, actual={2} in {3}",
                    signature, fileCrc, actualCrc, fileName));
            }
        }

		private void CheckFileLength(BufferReader reader)
		{
			if (length > reader.Remaining)
			{
				throw new EndOfBufferException(string.Format(
					"Attempted read of {0} bytes with only {1} bytes remaining in from {2}",
					length, reader.Remaining, reader.FileName));
			}
		}

        public void Read(BufferReader reader)
        {
            offset = reader.Offset;
            length = reader.ReadInt32();
            signature = reader.ReadSignature(2);
            fileCrc = (ushort)reader.ReadInt16();
			CheckFileLength(reader);
			actualCrc = reader.Crc16(length);
        }

        public void Dump(TextWriter writer, int indent)
        {
			if (indent > 0)
			{
				for (int x = 0; x < indent; x++)
					writer.Write('\t');
			}

            writer.Write(
                "Signature: {0} - Length: {1} - Offset: {2}",
                signature,
				length.ToString("X8"),
				offset.ToString("X8"));
			if (!IsCrcValid)
			{
				writer.Write(
					" - INVALID CRC: expected={0} actual={1})",
					fileCrc.ToString("X4"),
					actualCrc.ToString("X4"));
			}
			writer.WriteLine();
        }
    }
}
