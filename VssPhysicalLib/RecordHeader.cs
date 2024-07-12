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

using System.IO;

namespace Hpdi.VssPhysicalLib
{
    // #REVIEW this can probably be a struct
    /// <summary>
    /// Represents the header of a VSS record.
    /// </summary>
    /// <author>Trevor Robinson</author>
    public sealed class RecordHeader
    {
        public const int LENGTH = 8;

        public static bool IgnoreCrcErrors { get; set; }
            = true;

        public int Offset { get; private set; }
        public int Length { get; private set; }
        public string Signature { get; private set; }
        public ushort FileCrc { get; private set; }
        public ushort ActualCrc { get; private set; }
        public bool IsCrcValid => FileCrc == ActualCrc;

        public void CheckSignature(string expected)
        {
            if (Signature != expected)
            {
                throw new SourceSafe.Physical.Records.RecordNotFoundException(
                    $"Unexpected record signature: expected={expected}, actual={Signature}");
            }
        }
        public void LogInvalidSignature(string expected, string fileName)
        {
            System.Diagnostics.Debug.WriteLine(
                $"Unexpected record signature: expected={expected}, actual={Signature} at {Offset:X8} in {fileName}");
        }

        public void CheckCrc(string fileName)
        {
            if (!IsCrcValid)
            {
                if (IgnoreCrcErrors)
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"CRC error in {Signature} record: expected={FileCrc}, actual={ActualCrc} at {Offset:X8} in {fileName}");
                    return;
                }

                throw new RecordCrcException(this,
                    $"CRC error in {Signature} record: expected={FileCrc}, actual={ActualCrc} in {fileName}");
            }
        }

        private void CheckFileLength(BufferReader reader)
        {
            if (Length > reader.Remaining)
            {
                throw new SourceSafe.IO.EndOfBufferException(
                    $"Attempted read of {Length} bytes with only {reader.Remaining} bytes remaining in from {reader.FileName}");
            }
        }

        public void Read(BufferReader reader)
        {
            Offset = reader.Offset;
            Length = reader.ReadInt32();
            Signature = reader.ReadSignature(2);
            FileCrc = (ushort)reader.ReadInt16();
            CheckFileLength(reader);
            ActualCrc = reader.Crc16(Length);
        }

        public void Dump(TextWriter writer, int indent)
        {
            if (indent > 0)
            {
                for (int x = 0; x < indent; x++)
                    writer.Write('\t');
            }

            writer.Write($"Signature: {Signature} - Offset: {Offset:X8} - Length: {Length:X8}");
            if (!IsCrcValid)
            {
                writer.Write($" - INVALID CRC: expected={FileCrc:X4} actual={ActualCrc:X4})");
            }
            writer.WriteLine();
        }
    }
}
