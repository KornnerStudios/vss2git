/* Copyright 2017, Trapeze Poland sp. z o.o.
 * 
 * Author: Dariusz Bywalec
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
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Hpdi.Vss2Git
{
    public class EmailDictionaryFileReader
    {
        Dictionary <string, string> values;
        
		public EmailDictionaryFileReader(string path)
        {
			if (File.Exists(path))
			{			
				values = File.ReadLines(path)
				.Where(line => (!String.IsNullOrWhiteSpace(line) && !line.StartsWith("#")))
				.Select(line => line.Split(new char[] { '=' }, 2, 0))
				.ToDictionary(parts => parts[0].Trim(), parts => parts.Length > 1 ? parts[1].Trim() : null);
			}
        }
        
		public string GetValue(string name, string value=null)
        {
            if (values!=null && values.ContainsKey(name))
            {
                return values[name];
            }
            return value;
        }
    }
}
