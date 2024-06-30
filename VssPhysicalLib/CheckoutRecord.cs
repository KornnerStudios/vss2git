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

namespace Hpdi.VssPhysicalLib
{
    /// <summary>
    /// VSS record representing a file checkout.
    /// </summary>
    /// <author>Trevor Robinson</author>
    public sealed class CheckoutRecord : VssRecord
    {
        public const string SIGNATURE = "CF";

        public override string Signature => SIGNATURE;
        public string User { get; private set; }
        public DateTime DateTime { get; private set; }
        public string WorkingDir { get; private set; }
        public string Machine { get; private set; }
        public string Project { get; private set; }
        public string Comment { get; private set; }
        public int Revision { get; private set; }
        public int Flags { get; private set; }
        public bool Exclusive { get; private set; }
        public int PrevCheckoutOffset { get; private set; }
        public int ThisCheckoutOffset { get; private set; }
        public int Checkouts { get; private set; }

        public override void Read(BufferReader reader, RecordHeader header)
        {
            base.Read(reader, header);

            User = reader.ReadString(32);
            DateTime = reader.ReadDateTime();
            WorkingDir = reader.ReadString(260);
            Machine = reader.ReadString(32);
            Project = reader.ReadString(260);
            Comment = reader.ReadString(64);
            Revision = reader.ReadInt16();
            Flags = reader.ReadInt16();
            Exclusive = (Flags & 0x40) != 0;
            PrevCheckoutOffset = reader.ReadInt32();
            ThisCheckoutOffset = reader.ReadInt32();
            Checkouts = reader.ReadInt32();
        }

        public override void Dump(TextWriter writer, int indent)
        {
            string indentStr = DumpGetIndentString(indent);

            writer.Write(indentStr);
            writer.WriteLine("User: {0} @ {1}", User, DateTime);
            writer.Write(indentStr);
            writer.WriteLine("Working: {0}", WorkingDir);
            writer.Write(indentStr);
            writer.WriteLine("Machine: {0}", Machine);
            writer.Write(indentStr);
            writer.WriteLine("Project: {0}", Project);
            writer.Write(indentStr);
            writer.WriteLine("Comment: {0}", Comment);
            writer.Write(indentStr);
            writer.WriteLine("Revision: #{0:D3}", Revision);
            writer.Write(indentStr);
            writer.WriteLine("Flags: {0:X4}{1}", Flags,
                Exclusive ? " (exclusive)" : "");
            writer.Write(indentStr);
            writer.WriteLine("Prev checkout offset: {0:X6}", PrevCheckoutOffset);
            writer.Write(indentStr);
            writer.WriteLine("This checkout offset: {0:X6}", ThisCheckoutOffset);
            writer.Write(indentStr);
            writer.WriteLine("Checkouts: {0}", Checkouts);
        }
    }
}
