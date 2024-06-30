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

namespace Hpdi.VssPhysicalLib
{
    /// <summary>
    /// Structure used to store a VSS project or file name.
    /// </summary>
    /// <author>Trevor Robinson</author>
    public readonly struct VssName(short flags, string shortName, int nameFileOffset)
    {
        public bool IsProject
        {
            get { return (flags & 1) != 0; }
        }

        public string ShortName
        {
            get { return shortName; }
        }

        public int NameFileOffset
        {
            get { return nameFileOffset; }
        }
    }
}
