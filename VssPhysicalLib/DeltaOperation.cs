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
using System.IO;
using System.Text;

namespace Hpdi.VssPhysicalLib
{
    /// <summary>
    /// Enumeration of file revision delta commands.
    /// </summary>
    /// <author>Trevor Robinson</author>
    public enum DeltaCommand
    {
        WriteLog = 0, // write data from the log file
        WriteSuccessor = 1, // write data from the subsequent revision
        Stop = 2 // indicates the last operation
    }

    /// <summary>
    /// Represents a single delta operation for a file revision.
    /// </summary>
    /// <author>Trevor Robinson</author>
    public sealed class DeltaOperation
    {
        ArraySegment<byte> data; // WriteLog only

        public DeltaCommand Command { get; private set; }
        public int Offset { get; private set; }
        public int Length { get; private set; }
        public ArraySegment<byte> Data { get { return data; } }

        public static DeltaOperation WriteLog(byte[] data, int offset, int length)
        {
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
            var result = new DeltaOperation
            {
                Command = DeltaCommand.WriteSuccessor,
                Offset = offset,
                Length = length,
            };
            return result;
        }

        public void Read(BufferReader reader)
        {
            Command = (DeltaCommand)reader.ReadInt16();
            reader.Skip(2); // unknown
            Offset = reader.ReadInt32();
            Length = reader.ReadInt32();
            if (Command == DeltaCommand.WriteLog)
            {
                data = reader.GetBytes(Length);
            }
        }

        public static bool IncludeDataBytesInDump { get; set; } = false;
        public void Dump(TextWriter writer, int indent)
        {
            const int MAX_DATA_DUMP = 40;

            string indentStr = VssRecord.DumpGetIndentString(indent);

            writer.Write(indentStr);
            writer.Write("{0}: Offset={1}, Length={2}",
                Command, Offset, Length);
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
                writer.Write(", Data: {0}{1}", buf.ToString(), truncated ? "..." : "");
            }
            writer.WriteLine();
        }
    }
}
