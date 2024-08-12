﻿/* Copyright 2017, Trapeze Poland sp. z o.o.
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
using SourceSafe;
using SourceSafe.Logical;
using SourceSafe.Logical.Items;

namespace Hpdi.Vss2Git.GitActions
{
    /// <summary>
    /// Represents a VSS-revision-to-git file writer action.
    /// </summary>
    /// <author>Dariusz Bywalec</author>
    sealed class WriteFile : IGitAction
    {
        public static bool SleepThreadBeforeSetLastWriteTimeUtc { get; set; } = true;

        private readonly VssDatabase mDatabase;
        private readonly string mPhysicalName;
        private readonly int mVersion;
        private readonly string mDestinationPath;

        public WriteFile(VssDatabase database, string physicalName, int version, string destinationPath)
        {
            mDatabase = database;
            mPhysicalName = physicalName;
            mVersion = version;
            mDestinationPath = destinationPath;
        }

        public bool Run(
            SourceSafe.IO.SimpleLogger logger,
            IGitWrapper git,
            IGitStatistic stat)
        {
            logger.WriteLine($"Writing file: {mDestinationPath} ({mPhysicalName})@{mVersion}");

            VssFileItem item;
            VssFileItemRevision revision;
            Stream contents = null;
            try
            {
                item = (VssFileItem)mDatabase.GetItemByPhysicalName(mPhysicalName);
                revision = item.GetRevision(mVersion);
                contents = revision.GetContents();
            }
            catch (Exception e)
            {
                // log an error for missing data files or versions, but keep processing
                string message = SourceSafe.Exceptions.ExceptionFormatter.Format(e);
                logger.WriteLine($"ERROR: {message}");
                logger.WriteLine(e);
                return false;
            }

            if (null != contents)
            {
                // propagate exceptions here (e.g. disk full) to abort/retry/ignore
                using (contents)
                {
                    WriteStream(contents, mDestinationPath);
                }
            }

            // try to use the first revision (for this branch) as the create time,
            // since the item creation time doesn't seem to be meaningful
            DateTime createDateTime = item.Created;
            using (System.Collections.Generic.IEnumerator<VssFileItemRevision> revEnum = item.Revisions.GetEnumerator())
            {
                if (revEnum.MoveNext())
                {
                    createDateTime = revEnum.Current.DateTime;
                }
            }

            try
            {
                DateTime createDateTimeUtc = createDateTime.ConvertAmbiguousTimeToUtc(logger);
                DateTime revisionDateTimeUtc = revision.DateTime.ConvertAmbiguousTimeToUtc(logger);

                // set file creation and update timestamps
                File.SetCreationTimeUtc(mDestinationPath, createDateTimeUtc);
                if (SleepThreadBeforeSetLastWriteTimeUtc)
                {
                    // I've had one failure case for SetLastWriteTimeUtc because SetCreationTimeUtc had not yet completed.
                    // This was in 2018 on a HDD, perhaps less of a concern on SSDs?
                    System.Threading.Thread.Sleep(343);
                }
                File.SetLastWriteTimeUtc(mDestinationPath, revisionDateTimeUtc);
            }
            catch (Exception e)
            {
                // log an error for missing data files or versions, but keep processing
                string message = SourceSafe.Exceptions.ExceptionFormatter.Format(e);
                logger.WriteLine($"ERROR: {message}");
                logger.WriteLine(e);
                return false;
            }

            git.Add(mDestinationPath);

            return true;
        }

        private static void WriteStream(Stream inputStream, string path)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));

            using (var outputStream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                inputStream.CopyTo(outputStream);
            }
        }
    };
}
