using SourceSafe.Physical.DeltaDiff;
using SourceSafe.Physical.Files;
using SourceSafe.Physical.Records;

namespace SourceSafe.Logical.Items
{
    /// <summary>
    /// Represents a revision of a VSS file.
    /// </summary>
    public sealed class VssFileItemRevision : VssItemRevisionBase
    {
        public VssFileItem File => (VssFileItem)Item;

        public Stream GetContents()
        {
            Stream dataFile = new FileStream(Item.DataPath,
                FileMode.Open, FileAccess.Read, FileShare.Read);

            VssPhysicalFile itemPhysicalFile = Item.PhysicalFile;
            Physical.Revisions.RevisionRecordBase? lastRev = itemPhysicalFile.GetLastRevision();
            if (lastRev != null)
            {
                IEnumerable<DeltaOperation>? deltaOps = null;
                while (lastRev != null && lastRev.Revision > this.Version)
                {
                    if (lastRev is Physical.Revisions.BranchRevisionRecord branchRev)
                    {
                        int branchRevId = branchRev.Revision;
                        string itemPath = Item.Database.GetDataPath(branchRev.BranchFile);
                        itemPhysicalFile = new(itemPath, Item.Database.Encoding);
                        lastRev = itemPhysicalFile.GetLastRevision();
                        while (lastRev != null && lastRev.Revision >= branchRevId)
                        {
                            lastRev = itemPhysicalFile.GetPreviousRevision(lastRev);
                        }
                    }
                    else
                    {
                        if (lastRev is Physical.Revisions.EditRevisionRecord editRev)
                        {
                            DeltaRecord? delta = itemPhysicalFile.GetPreviousDelta(editRev);
                            if (delta != null)
                            {
                                IEnumerable<DeltaOperation> curDeltaOps = delta.Operations;
                                deltaOps = (deltaOps == null) ? curDeltaOps :
                                    DeltaUtil.Merge(deltaOps, curDeltaOps);
                            }
                        }
                        lastRev = itemPhysicalFile.GetPreviousRevision(lastRev);
                    }
                }

                if (deltaOps != null)
                {
                    dataFile = new DeltaStream(dataFile, deltaOps);
                }
            }

            return dataFile;
        }

        internal VssFileItemRevision(
            VssItemBase item,
            Physical.Revisions.RevisionRecordBase revision,
            CommentRecord comment)
            : base(item, revision, comment)
        {
        }
    };
}
