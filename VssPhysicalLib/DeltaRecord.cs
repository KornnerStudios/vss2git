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

using System.Collections.Generic;
using System.IO;
using SourceSafe.Physical.DeltaDiff;

namespace Hpdi.VssPhysicalLib
{
    /// <summary>
    /// VSS record representing a reverse-delta for a file revision.
    /// </summary>
    /// <author>Trevor Robinson</author>
    public sealed class DeltaRecord : VssRecord
    {
        public const string SIGNATURE = "FD";

        private readonly List<DeltaOperation> operations = [];

        public override string Signature => SIGNATURE;
        public IEnumerable<DeltaOperation> Operations => operations;

        public static bool ReadCheckForMissingStopCommands { get; set; } = false;
        public override void Read(SourceSafe.IO.VssBufferReader reader, RecordHeader header)
        {
            base.Read(reader, header);

            int dataStartOffset = header.Offset + RecordHeader.LENGTH;
            int dataEndOffset = dataStartOffset + header.Length;
#if DEBUG
            bool encounteredStop = false;
#endif // DEBUG

            for (int offset = reader.Offset; offset < dataEndOffset; offset = reader.Offset)
            {
                DeltaOperation operation = new();
                operation.Read(reader);
                if (operation.Command == DeltaCommand.Stop)
                {
#if DEBUG
                    encounteredStop = true;
#endif // DEBUG
                    break;
                }
                operations.Add(operation);
            }

#if DEBUG
            if (ReadCheckForMissingStopCommands && !encounteredStop)
            {
                "".ToString(); // place a breakpoint as needed
            }
#endif // DEBUG
        }

        public override void Dump(TextWriter writer, int indent)
        {
            foreach (DeltaOperation operation in operations)
            {
                operation.Dump(writer, indent);
            }
        }
    }
}
