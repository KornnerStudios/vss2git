using System.Collections;

namespace SourceSafe.Logical.Items
{
    partial class VssItemBase
    {
        protected sealed class VssRevisionsEnumeration<ItemT, RevisionT> : IEnumerable<RevisionT>
            where ItemT : VssItemBase
            where RevisionT : VssItemRevisionBase
        {
            private readonly ItemT mItem;

            internal VssRevisionsEnumeration(ItemT item) => mItem = item;

            public IEnumerator<RevisionT> GetEnumerator()
                => new VssRevisionEnumerator<ItemT, RevisionT>(mItem);

            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        };

        private sealed class VssRevisionEnumerator<ItemT, RevisionT> : IEnumerator<RevisionT>
            where ItemT : VssItemBase
            where RevisionT : VssItemRevisionBase
        {
            private readonly ItemT mItem;
            private Physical.Revisions.RevisionRecordBase? mRevisionRecord;
            private RevisionT? mRevision;
            private bool mBeforeFirst = true;
            private int mRevisionIndex = -1;

            internal VssRevisionEnumerator(ItemT item) => mItem = item;

            public void Dispose()
            {
            }

            public void Reset()
            {
                mBeforeFirst = true;
            }

            public bool MoveNext()
            {
                mRevision = null;
                int nextRevisionIndex = mRevisionIndex + 1;

                if (mBeforeFirst)
                {
                    mRevisionRecord = mItem.PhysicalFile.GetFirstRevision();
                    mBeforeFirst = false;
                }
                else if (mRevisionRecord != null)
                {
                    mRevisionRecord = mItem.PhysicalFile.GetNextRevision(mRevisionRecord);
                }

                if (mRevisionRecord != null)
                {
                    mRevisionIndex = nextRevisionIndex;
                }

                return mRevisionRecord != null;
            }

            public RevisionT Current
            {
                get
                {
                    if (mRevisionRecord == null)
                    {
                        throw new InvalidOperationException();
                    }

                    if (mRevision == null)
                    {
                        mRevision = (RevisionT)mItem.CreateRevision(mRevisionRecord);
                    }

                    return mRevision;
                }
            }

            object IEnumerator.Current => Current;
        };
    };
}
