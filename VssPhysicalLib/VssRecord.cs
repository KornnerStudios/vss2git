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

using SourceSafe.IO;
using System.IO;

namespace Hpdi.VssPhysicalLib
{
    /// <summary>
    /// Base class for VSS records.
    /// </summary>
    /// <author>Trevor Robinson</author>
    public abstract class VssRecord
    {
        public abstract string Signature { get; }
        public RecordHeader Header { get; private set; }

        public virtual void Read(VssBufferReader reader, RecordHeader header)
        {
            Header = header;
        }

        public abstract void Dump(TextWriter writer, int indent);

        public static string DumpGetIndentString(int indent)
        {
            return SourceSafe.IO.OutputUtil.GetIndentString(indent);
        }
    }
}
