using System.Collections;
using SourceSafe.Physical;

namespace SourceSafe.Logical.Items
{
    partial class VssProjectItem
    {
        private sealed class VssProjectsEnumeration : IEnumerable<VssProjectItem>
        {
            private readonly VssProjectItem mProject;

            internal VssProjectsEnumeration(VssProjectItem project) => mProject = project;

            public IEnumerator<VssProjectItem> GetEnumerator()
                => new VssItemEnumerator<VssProjectItem>(mProject, EnumerationItemTypeFlags.Project, mProject.DataPath);

            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        };

        private sealed class VssFilesEnumeration : IEnumerable<VssFileItem>
        {
            private readonly VssProjectItem mProject;

            internal VssFilesEnumeration(VssProjectItem project) => mProject = project;

            public IEnumerator<VssFileItem> GetEnumerator()
                => new VssItemEnumerator<VssFileItem>(mProject, EnumerationItemTypeFlags.File, mProject.DataPath);

            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        };

        [Flags]
        private enum EnumerationItemTypeFlags
        {
            Project = VssItemType.Project,
            File = VssItemType.File,
            Any = Project | File
        };

        private sealed class VssItemEnumerator<T> : IEnumerator<T>
            where T : VssItemBase
        {
            private readonly VssProjectItem mProject;
            private readonly EnumerationItemTypeFlags mItemTypeFlags;
            private readonly Physical.Projects.ProjectEntryFile mEntryFile;
            private Physical.Projects.ProjectEntryRecord? mEntryRecord;
            private VssItemBase? mEntryItem;
            private bool mBeforeFirst = true;

            internal VssItemEnumerator(VssProjectItem project, EnumerationItemTypeFlags itemTypes, string entryFilePath)
            {
                mProject = project;
                mItemTypeFlags = itemTypes;
                mEntryFile = new(entryFilePath, project.Database.Encoding);
            }

            public void Dispose()
            {
            }

            public void Reset()
            {
                mBeforeFirst = true;
            }

            public bool MoveNext()
            {
                mEntryItem = null;
                do
                {
                    mEntryRecord = mBeforeFirst ? mEntryFile.GetFirstEntry() : mEntryFile.GetNextEntry();
                    mBeforeFirst = false;
                }
                while (mEntryRecord != null && ((int)mItemTypeFlags & (int)mEntryRecord.ItemType) == 0);
                return mEntryRecord != null;
            }

            public T Current
            {
                get
                {
                    if (mEntryRecord == null)
                    {
                        throw new InvalidOperationException();
                    }

                    if (mEntryItem == null)
                    {
                        string physicalName = mEntryRecord.PhysicalNameAllUpperCase;
                        string logicalName = mProject.Database.GetFullName(mEntryRecord.Name);
                        if (mEntryRecord.IsProject)
                        {
                            mEntryItem = mProject.Database.OpenProject(mProject, physicalName, logicalName);
                        }
                        else
                        {
                            mEntryItem = mProject.Database.OpenFile(physicalName, logicalName);
                        }
                    }

                    return (T)mEntryItem;
                }
            }

            object IEnumerator.Current => Current;
        };
    };
}
