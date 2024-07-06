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
    /// VSS header record for the name file.
    /// </summary>
    /// <author>Trevor Robinson</author>
    public sealed class NameHeaderRecord : VssRecord
    {
        public const string SIGNATURE = "HN";

        public override string Signature => SIGNATURE;
        public int EofOffset { get; private set; }

        public override void Read(BufferReader reader, RecordHeader header)
        {
            base.Read(reader, header);

            reader.SkipAssumedToBeAllZeros(16); // reserved; always 0
            EofOffset = reader.ReadInt32();
            reader.SkipAssumedToBeAllZeros(60); // remaining reserved; always 0
        }

        public override void Dump(TextWriter writer, int indent)
        {
            string indentStr = DumpGetIndentString(indent);

            writer.Write(indentStr);
            writer.WriteLine($"EOF offset: {EofOffset:X6}");
        }
    }
}
