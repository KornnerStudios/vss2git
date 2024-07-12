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
using Hpdi.VssPhysicalLib;
using SourceSafe.Physical.DeltaDiff;

namespace Hpdi.VssLogicalLib
{
    /// <summary>
    /// Represents a revision of a VSS file.
    /// </summary>
    /// <author>Trevor Robinson</author>
    public class VssFileRevision : VssRevision
    {
        public VssFile File => (VssFile)Item;

        public Stream GetContents()
        {
            Stream dataFile = new FileStream(item.DataPath,
                FileMode.Open, FileAccess.Read, FileShare.Read);

            ItemFile itemFile = item.ItemFile;
            RevisionRecord lastRev = itemFile.GetLastRevision();
            if (lastRev != null)
            {
                IEnumerable<DeltaOperation> deltaOps = null;
                while (lastRev != null && lastRev.Revision > this.Version)
                {
                    if (lastRev is BranchRevisionRecord branchRev)
                    {
                        int branchRevId = branchRev.Revision;
                        string itemPath = item.Database.GetDataPath(branchRev.BranchFile);
                        itemFile = new ItemFile(itemPath, item.Database.Encoding);
                        lastRev = itemFile.GetLastRevision();
                        while (lastRev != null && lastRev.Revision >= branchRevId)
                        {
                            lastRev = itemFile.GetPreviousRevision(lastRev);
                        }
                    }
                    else
                    {
                        if (lastRev is EditRevisionRecord editRev)
                        {
                            DeltaRecord delta = itemFile.GetPreviousDelta(editRev);
                            if (delta != null)
                            {
                                IEnumerable<DeltaOperation> curDeltaOps = delta.Operations;
                                deltaOps = (deltaOps == null) ? curDeltaOps :
                                    DeltaUtil.Merge(deltaOps, curDeltaOps);
                            }
                        }
                        lastRev = itemFile.GetPreviousRevision(lastRev);
                    }
                }

                if (deltaOps != null)
                {
                    dataFile = new DeltaStream(dataFile, deltaOps);
                }
            }

            return dataFile;
        }

        internal VssFileRevision(VssItem item, RevisionRecord revision, CommentRecord comment)
            : base(item, revision, comment)
        {
        }
    }
}
