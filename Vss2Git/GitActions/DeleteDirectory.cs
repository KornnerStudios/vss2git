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

using Hpdi.VssLogicalLib;
using System;
using System.IO;

namespace Hpdi.Vss2Git.GitActions
{
    /// <summary>
    /// Represents a delete directory action.
    /// </summary>
    /// <author>Dariusz Bywalec</author>
    class DeleteDirectory : IGitAction
    {
        private readonly string path;
        private readonly bool containsFiles;

        public DeleteDirectory(string path, bool containsFiles)
        {
            this.path = path;
            this.containsFiles = containsFiles;
        }

        public bool Run(Logger logger, IGitWrapper git, IGitStatistic stat)
        {
            logger.WriteLine("Deleting directory: {0}", path);

            if (containsFiles)
            {
                git.RemoveDir(path, true);
            }
            else
            {
                // git doesn't care about directories with no files
                Directory.Delete(path, true);
            }

            return true;
        }
    }
}
