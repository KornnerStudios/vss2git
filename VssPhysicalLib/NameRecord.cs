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
    /// Enumeration of the kinds of VSS logical item names.
    /// </summary>
    /// <author>Trevor Robinson</author>
    public enum NameKind
    {
        Dos = 1, // DOS 8.3 filename
        Long = 2, // Win95/NT long filename
        MacOS = 3, // Mac OS 9 and earlier 31-character filename
        Project = 10 // VSS project name
    }

    /// <summary>
    /// VSS record containing the logical names of an object in particular contexts.
    /// </summary>
    /// <author>Trevor Robinson</author>
    public sealed class NameRecord : VssRecord
    {
        public const string SIGNATURE = "SN";
        NameKind[] kinds;
        string[] names;

        public override string Signature => SIGNATURE;
        public int KindCount { get; private set; }

        public int IndexOf(NameKind kind)
        {
            for (int i = 0; i < KindCount; ++i)
            {
                if (kinds[i] == kind)
                {
                    return i;
                }
            }
            return -1;
        }

        public NameKind GetKind(int index)
        {
            return kinds[index];
        }

        public string GetName(int index)
        {
            return names[index];
        }

        public override void Read(BufferReader reader, RecordHeader header)
        {
            base.Read(reader, header);

            KindCount = reader.ReadInt16();
            reader.SkipUnknown(2);
            kinds = new NameKind[KindCount];
            names = new string[KindCount];
            int baseOffset = reader.Offset + (KindCount * 4);
            for (int i = 0; i < KindCount; ++i)
            {
                kinds[i] = (NameKind)reader.ReadInt16();
                short nameOffset = reader.ReadInt16();
                int saveOffset = reader.Offset;
                try
                {
                    reader.Offset = baseOffset + nameOffset;
                    names[i] = reader.ReadString(reader.RemainingSize);
                }
#if DEBUG // #REVIEW why did the original author wrap this in a try/catch block?
                catch (System.Exception e)
                {
                    e.ToString();
                }
#endif // DEBUG
                finally
                {
                    reader.Offset = saveOffset;
                }
            }
        }

        public override void Dump(TextWriter writer, int indent)
        {
            string indentStr = DumpGetIndentString(indent);
            for (int i = 0; i < KindCount; ++i)
            {
                writer.Write(indentStr);
                writer.WriteLine("{0} name: {1}", kinds[i], names[i]);
            }
        }
    }
}
