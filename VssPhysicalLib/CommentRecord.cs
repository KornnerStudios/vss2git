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
    /// <summary>
    /// VSS record representing a comment message.
    /// </summary>
    /// <author>Trevor Robinson</author>
    public sealed class CommentRecord : VssRecord
    {
        public const string SIGNATURE = "MC";

        public override string Signature => SIGNATURE;
        public string Comment { get; private set; }

        public override void Read(BufferReader reader, RecordHeader header)
        {
            base.Read(reader, header);

            Comment = reader.ReadString(reader.RemainingSize);
        }

        public override void Dump(TextWriter writer, int indent)
        {
            string indentStr = DumpGetIndentString(indent);

            writer.Write(indentStr);
            writer.WriteLine("{0}", Comment);
        }
    }
}
