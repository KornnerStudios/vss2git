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
using System.IO;

namespace Hpdi.Vss2Git.GitActions
{
    /// <summary>
    /// Represents a generic git move action.
    /// </summary>
    /// <author>Dariusz Bywalec</author>
    abstract class MoveAction : IGitAction
    {
        protected readonly string sourcePath;
        protected readonly string targetPath;

        public MoveAction(string sourcePath, string targetPath)
        {
            this.sourcePath = sourcePath;
            this.targetPath = targetPath;
        }

        public abstract bool Run(
            SourceSafe.IO.SimpleLogger logger,
            IGitWrapper git,
            IGitStatistic stat);

        protected delegate void RenameDelegate(string sourcePath, string destPath);

        protected static void CaseSensitiveRename(string sourcePath, string destPath, RenameDelegate renamer)
        {
            if (sourcePath.Equals(destPath, StringComparison.OrdinalIgnoreCase))
            {
                // workaround for case-only renames on case-insensitive file systems:

                string sourceDir = Path.GetDirectoryName(sourcePath);
                string sourceFile = Path.GetFileName(sourcePath);
                string destDir = Path.GetDirectoryName(destPath);
                string destFile = Path.GetFileName(destPath);

                if (sourceDir != destDir)
                {
                    // recursively rename containing directories that differ in case
                    CaseSensitiveRename(sourceDir, destDir, renamer);

                    // fix up source path based on renamed directory
                    sourcePath = Path.Combine(destDir, sourceFile);
                }

                if (sourceFile != destFile)
                {
                    // use temporary filename to rename files that differ in case
                    string tempPath = sourcePath + ".mvtmp";
                    CaseSensitiveRename(sourcePath, tempPath, renamer);
                    CaseSensitiveRename(tempPath, destPath, renamer);
                }
            }
            else
            {
                renamer(sourcePath, destPath);
            }
        }
    }
}
